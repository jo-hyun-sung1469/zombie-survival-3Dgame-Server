using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
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

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = "Zombie Survival signup verification code",
            Body = $"Your Zombie Survival signup verification code is {code}. It expires soon.",
            IsBodyHtml = false
        };
        message.To.Add(email);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}
