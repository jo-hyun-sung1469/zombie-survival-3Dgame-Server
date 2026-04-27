using zombie_servival_3Dgame_Server.Contracts.Auth;

namespace zombie_servival_3Dgame_Server.Auth;

public interface IJwtTokenService
{
    LoginResponse CreateToken(AuthenticatedUser user);
}
