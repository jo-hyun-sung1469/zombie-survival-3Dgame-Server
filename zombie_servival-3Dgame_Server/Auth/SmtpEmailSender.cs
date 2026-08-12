using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using zombie_survival_3Dgame_Server.Options;

namespace zombie_survival_3Dgame_Server.Auth;

public sealed class SmtpEmailSender(IOptions<SmtpEmailOptions> smtpEmailOptions) : IEmailSender
{
    private readonly SmtpEmailOptions _options = smtpEmailOptions.Value;

    public async Task SendRegisterVerificationCodeAsync(string email, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            throw new InvalidOperationException("SMTP email settings are missing.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Zombie Survival 회원가입 인증 코드";
        message.Body = new TextPart("plain")
        {
            Text = $"Zombie Survival 회원가입 인증 코드는 {code}입니다. 인증 코드는 곧 만료됩니다."
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _options.Host,
            _options.Port,
            GetSecureSocketOptions(_options),
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private static SecureSocketOptions GetSecureSocketOptions(SmtpEmailOptions options)
    {
        if (!options.EnableSsl)
        {
            throw new InvalidOperationException("SMTP transport encryption must be enabled.");
        }

        return options.Port == 465
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;
    }
}
