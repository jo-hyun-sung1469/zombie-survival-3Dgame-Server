using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;
using zombie_survival_3Dgame_Server.Auth;
using zombie_survival_3Dgame_Server.Common;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Firearm;
using zombie_survival_3Dgame_Server.Firearm.Configuration;
using zombie_survival_3Dgame_Server.Gacha;
using zombie_survival_3Dgame_Server.Inventory;
using zombie_survival_3Dgame_Server.Options;
using zombie_survival_3Dgame_Server.Player;
using zombie_survival_3Dgame_Server.WeaponUpgrade;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("player-defaults.local.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<EmailAuthOptions>()
    .Bind(builder.Configuration.GetSection(EmailAuthOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<SmtpEmailOptions>()
    .Bind(builder.Configuration.GetSection(SmtpEmailOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<GachaOptions>()
    .Bind(builder.Configuration.GetSection(GachaOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<WeaponUpgradeOptions>()
    .Bind(builder.Configuration.GetSection(WeaponUpgradeOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<PlayerOptions>()
    .Bind(builder.Configuration.GetSection(PlayerOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<PlayerDefaultDataOptions>()
    .Bind(builder.Configuration.GetSection(PlayerDefaultDataOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<EmailAuthOptions>, EmailAuthOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<SmtpEmailOptions>, SmtpEmailOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<GachaOptions>, GachaOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<WeaponUpgradeOptions>, WeaponUpgradeOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<PlayerOptions>, PlayerOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<PlayerDefaultDataOptions>, PlayerDefaultDataOptionsValidator>();
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                 ?? throw new InvalidOperationException("JWT settings are missing.");
if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey))
{
    throw new InvalidOperationException("Jwt__SecretKey environment variable is missing.");
}

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<PersistenceConflictExceptionHandler>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(
        RateLimitPolicyNames.EmailCodeSend,
        context => CreateFixedWindowPartition(GetRemotePartitionKey(context), 3, TimeSpan.FromMinutes(10)));
    options.AddPolicy(
        RateLimitPolicyNames.EmailCodeVerify,
        context => CreateFixedWindowPartition(GetRemotePartitionKey(context), 10, TimeSpan.FromMinutes(5)));
    options.AddPolicy(
        RateLimitPolicyNames.Register,
        context => CreateFixedWindowPartition(GetRemotePartitionKey(context), 5, TimeSpan.FromMinutes(10)));
    options.AddPolicy(
        RateLimitPolicyNames.Login,
        context => CreateFixedWindowPartition(GetRemotePartitionKey(context), 10, TimeSpan.FromMinutes(5)));
    options.AddPolicy(
        RateLimitPolicyNames.PlayerMutation,
        context => CreateFixedWindowPartition(GetPlayerPartitionKey(context), 20, TimeSpan.FromMinutes(1)));
});
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    var databaseHost = builder.Configuration["Database:Host"];
    var databaseName = builder.Configuration["Database:Name"];
    var databaseUser = builder.Configuration["Database:User"];
    var databaseCredential = builder.Configuration["Database:Credential"];

    if (string.IsNullOrWhiteSpace(databaseHost)
        || string.IsNullOrWhiteSpace(databaseName)
        || string.IsNullOrWhiteSpace(databaseUser)
        || string.IsNullOrWhiteSpace(databaseCredential))
    {
        throw new InvalidOperationException("The Database connection settings are incomplete.");
    }

    var databaseSslModeName = builder.Configuration["Database:SslMode"]
                              ?? (IsInternalDatabaseHost(databaseHost) ? "Disabled" : "VerifyFull");
    if (!Enum.TryParse<MySqlSslMode>(databaseSslModeName, true, out var databaseSslMode))
    {
        throw new InvalidOperationException("Database:SslMode is invalid.");
    }

    var databaseConnection = new MySqlConnectionStringBuilder
    {
        Server = databaseHost,
        Port = builder.Configuration.GetValue<uint?>("Database:Port") ?? 3306,
        Database = databaseName,
        UserID = databaseUser,
        CharacterSet = "utf8mb4",
        SslMode = databaseSslMode,
        AllowPublicKeyRetrieval =
            IsInternalDatabaseHost(databaseHost) && databaseSslMode == MySqlSslMode.Disabled
    };
    databaseConnection["Pwd"] = databaseCredential;
    connectionString = databaseConnection.ConnectionString;
}

ValidateDatabaseTransport(builder.Environment, new MySqlConnectionStringBuilder(connectionString));
builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseMySql(connectionString, GameDbContextFactory.MySqlServerVersion));
builder.Services.AddHealthChecks()
    .AddDbContextCheck<GameDbContext>(
        "mysql",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IAuthService, DbAuthService>();
builder.Services.AddScoped<IPlayerSaveDataStore, PlayerSaveDataStore>();
builder.Services.AddScoped<IPlayerDefaultDataRepairService, PlayerDefaultDataRepairService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IGachaService, GachaService>();
builder.Services.AddScoped<IFirearmService, FirearmService>();
builder.Services.AddScoped<IWeaponUpgradeService, WeaponUpgradeService>();
builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<IPlayerStatUpgradeService, PlayerStatUpgradeService>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    await dbContext.Database.MigrateAsync();
    await FirearmCatalogSeeder.UpsertAsync(dbContext, DefaultFirearmCatalog.Items);
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsProduction())
{
    app.UseForwardedHeaders();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
}).AllowAnonymous();
app.MapControllers();

app.Run();

static RateLimitPartition<string> CreateFixedWindowPartition(
    string partitionKey,
    int permitLimit,
    TimeSpan window)
{
    return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey,
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0,
            AutoReplenishment = true
        });
}

static string GetRemotePartitionKey(HttpContext context)
{
    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

static string GetPlayerPartitionKey(HttpContext context)
{
    return context.User.FindFirst("userId")?.Value ?? GetRemotePartitionKey(context);
}

static bool IsInternalDatabaseHost(string? host)
{
    return string.Equals(host, "mysql", StringComparison.OrdinalIgnoreCase);
}

static void ValidateDatabaseTransport(IHostEnvironment environment, MySqlConnectionStringBuilder settings)
{
    if (settings.SslMode == MySqlSslMode.Preferred)
    {
        throw new InvalidOperationException(
            "Database SSL mode Preferred is not allowed because it permits TLS downgrade.");
    }

    if (environment.IsProduction()
        && !IsInternalDatabaseHost(settings.Server)
        && settings.SslMode != MySqlSslMode.VerifyFull)
    {
        throw new InvalidOperationException("External production databases must use SslMode=VerifyFull.");
    }
}
