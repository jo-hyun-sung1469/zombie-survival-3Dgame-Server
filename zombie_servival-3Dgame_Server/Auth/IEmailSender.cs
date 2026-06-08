namespace zombie_survival_3Dgame_Server.Auth;

public interface IEmailSender
{
    Task SendRegisterVerificationCodeAsync(string email, string code, CancellationToken cancellationToken);
}
