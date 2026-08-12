using zombie_survival_3Dgame_Server.Contracts.Player;

namespace zombie_survival_3Dgame_Server.Player;

//강화 결과(처리 성공 여부)를 서버 내부에 반환하는 클래스(모델)
public sealed class PlayerStatUpgradeResult
{
    public required PlayerStatUpgradeStatus Status { get; init; }
    public int RequiredGold { get; init; }
    public int CurrentGold { get; init; }
    public int CurrentUpgradeLevel { get; init; }
    public int MaxLevel { get; init; }

    //여기서 이름이나 강화량을 반환
    public UpgradePlayerStatResponse? Response { get; init; }
}
