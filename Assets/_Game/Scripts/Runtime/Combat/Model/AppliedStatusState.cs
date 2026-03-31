namespace SM.Combat.Model;

public sealed record AppliedStatusState(
    string StatusId,
    float RemainingSeconds,
    float DurationSeconds,
    float Magnitude,
    int Stacks = 1);

public sealed record ControlResistWindowState(
    float RemainingSeconds,
    float ResistMultiplier);
