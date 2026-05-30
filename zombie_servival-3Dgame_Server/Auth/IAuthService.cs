using zombie_survival_3Dgame_Server.Auth.Models;
using zombie_survival_3Dgame_Server.Contracts.Auth;

namespace zombie_survival_3Dgame_Server.Auth;

public interface IAuthService
{
    Task<AuthenticatedUser?> ValidateCredentialsAsync(string userName, string password, CancellationToken cancellationToken);
    Task<RegisterResponse?> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
}
