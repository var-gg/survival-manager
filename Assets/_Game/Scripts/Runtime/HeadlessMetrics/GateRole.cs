namespace SM.HeadlessMetrics;

/// <summary>게이트 실패가 release를 차단하는지, 진단 신호로만 남는지 구분한다.</summary>
public enum GateRole
{
    Hard,
    Diagnostic,
}

internal static class GateRoleWire
{
    public static string ToWireValue(this GateRole role)
        => role == GateRole.Hard ? "hard" : "diagnostic";
}
