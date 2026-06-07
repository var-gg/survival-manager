using SM.Combat.Services;

namespace SM.Combat.Model;

// Phase 2.2c: 권위는 정수 틱(Remaining/DurationTicks). RemainingSeconds/DurationSeconds는 read-model·UI·canonical
// hash용 초 projection(틱×TickSeconds) — sim 분기는 틱 필드를 본다.
public sealed record AppliedStatusState(
    string StatusId,
    int RemainingTicks,
    int DurationTicks,
    float Magnitude,
    int Stacks = 1)
{
    public float RemainingSeconds => RemainingTicks * BattleTickMath.TickSeconds;
    public float DurationSeconds => DurationTicks * BattleTickMath.TickSeconds;
}

public sealed record ControlResistWindowState(
    int RemainingTicks,
    float ResistMultiplier);
