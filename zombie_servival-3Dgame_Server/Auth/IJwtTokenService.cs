using zombie_survival_3Dgame_Server.Auth.Models;
using zombie_survival_3Dgame_Server.Contracts.Auth;

namespace zombie_survival_3Dgame_Server.Auth;

public interface IJwtTokenService
{
    LoginResponse CreateToken(AuthenticatedUser user);
}
