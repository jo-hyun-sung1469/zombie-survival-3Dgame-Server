using zombie_servival_3Dgame_Server.Contracts.Auth;

namespace zombie_servival_3Dgame_Server.Auth;

public interface IAuthService
{
    Task<AuthenticatedUser?> ValidateCredentialsAsync(string userName, string password, CancellationToken cancellationToken);
    Task<RegisterResponse?> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
}
