using System;
using System.Collections.Generic;

namespace SM.Combat.Model;

/// <summary>
/// 진형 consequence 상태 방출의 전투 단위 ICD 장부. 스크린 반격은 스크리너별, 후방 협격은 표적별로
/// 마지막 방출 step을 기록한다. Dictionary는 키 조회만 하고 열거하지 않아 결정 순서에 관여하지 않는다.
/// seed 재시뮬로 재구성되므로 <see cref="CombatComboLedger"/>와 같이 직렬화하지 않는다.
/// </summary>
public sealed class FormationStatusLedger
{
    private readonly Dictionary<string, int> _riposteLastEmittedStep = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _flankLastEmittedStep = new(StringComparer.Ordinal);

    public bool TryClaimRiposte(string screeningUnitId, int step, int icdTicks)
        => TryClaim(_riposteLastEmittedStep, screeningUnitId, step, icdTicks);

    public bool TryClaimFlank(string targetUnitId, int step, int icdTicks)
        => TryClaim(_flankLastEmittedStep, targetUnitId, step, icdTicks);

    private static bool TryClaim(Dictionary<string, int> ledger, string unitId, int step, int icdTicks)
    {
        if (string.IsNullOrWhiteSpace(unitId)
            || ledger.TryGetValue(unitId, out var emittedStep) && step - emittedStep < icdTicks)
        {
            return false;
        }

        ledger[unitId] = step;
        return true;
    }
}
