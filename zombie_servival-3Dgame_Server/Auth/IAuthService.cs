using zombie_survival_3Dgame_Server.Contracts.Auth;
using zombie_survival_3Dgame_Server.Auth.Models;

namespace zombie_survival_3Dgame_Server.Auth;

public interface IAuthService
{
    Task<RegisterEmailCodeResult> SendRegisterEmailCodeAsync(
        SendRegisterEmailCodeRequest request,
        CancellationToken cancellationToken);

    Task<RegisterEmailVerificationResult> VerifyRegisterEmailCodeAsync(
        VerifyRegisterEmailCodeRequest request,
        CancellationToken cancellationToken);

    Task<AuthenticatedUser?> ValidateCredentialsAsync(string userName, string password, CancellationToken cancellationToken);
    Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
}
