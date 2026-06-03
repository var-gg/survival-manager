using System;

namespace SM.Persistence.Abstractions.Models;

/// <summary>
/// 한 정치 세력에 대한 플레이어의 평판(trust). ludonarrative 루프 정치적 전환 —
/// warrant(faction mandate) 이행/위반이 이 값을 바꾸고, (slice 2) 이 값이 다음 전투 조건을 바꾼다.
/// 평판은 run 간 지속되므로 run overlay가 아니라 profile truth(SaveProfile)다. ADR-0028.
/// </summary>
[Serializable]
public sealed class FactionStandingRecord
{
    /// <summary>정치 세력 stable id(표시명은 별도 label — ID/label 분리).</summary>
    public string FactionId = string.Empty;

    /// <summary>누적 평판. warrant satisfied → 상승, failed → 하락.</summary>
    public int Trust;
}
