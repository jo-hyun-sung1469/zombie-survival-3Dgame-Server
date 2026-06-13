using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using zombie_survival_3Dgame_Server.Auth;
using zombie_survival_3Dgame_Server.Data;
using zombie_survival_3Dgame_Server.Firearm;
using zombie_survival_3Dgame_Server.Firearm.Configuration;
using zombie_survival_3Dgame_Server.Gacha;
using zombie_survival_3Dgame_Server.Inventory;
using zombie_survival_3Dgame_Server.Options;
using zombie_survival_3Dgame_Server.Player;
using zombie_survival_3Dgame_Server.WeaponUpgrade;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<EmailAuthOptions>(builder.Configuration.GetSection(EmailAuthOptions.SectionName));
builder.Services.Configure<SmtpEmailOptions>(builder.Configuration.GetSection(SmtpEmailOptions.SectionName));
builder.Services.Configure<GachaOptions>(builder.Configuration.GetSection(GachaOptions.SectionName));
builder.Services.Configure<WeaponUpgradeOptions>(builder.Configuration.GetSection(WeaponUpgradeOptions.SectionName));
builder.Services.Configure<PlayerOptions>(builder.Configuration.GetSection(PlayerOptions.SectionName));
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
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings__DefaultConnection environment variable is missing.");
}

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IAuthService, DbAuthService>();
builder.Services.AddScoped<IPlayerSaveDataStore, PlayerSaveDataStore>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IGachaService, GachaService>();
builder.Services.AddScoped<IFirearmService, FirearmService>();
builder.Services.AddScoped<IWeaponUpgradeService, WeaponUpgradeService>();
builder.Services.AddSingleton<IPlayerService, PlayerService>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    dbContext.Database.EnsureCreated();
    await FirearmCatalogSeeder.UpsertAsync(dbContext, DefaultFirearmCatalog.Items);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
