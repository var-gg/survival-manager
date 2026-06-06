using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SM.Combat.Model;

namespace SM.Combat.Services;

/// <summary>
/// CanonicalStateHashV1 (ADR-0029 / docs/03_architecture/deterministic-sim-and-fixed-point-migration.md
/// Phase 0). Authoritative <see cref="BattleState"/>를 고정 필드 순서·정렬된 collection·명시적
/// little-endian raw로 직렬화한 뒤 FNV-1a 64-bit digest를 만든다.
///
/// <para><b>관측 전용</b>: state를 변경하지 않으며, 아직 어떤 sim/replay 경로에도 연결되지 않는다
/// (Phase 0 "behavior 변화 0" — 기존 replay <c>StateHash</c> 계산은 그대로). cross-platform 발산을
/// 잡는 골든 해시 오라클의 토대다.</para>
///
/// <para><b>현 단계</b>는 float raw bits를 먹는다(same-binary 재현부터 확보). Phase 1+에서 동일
/// 직렬화 순서로 Fixed raw int을 먹도록 교체하면 hash 인프라·필드 순서는 그대로 유지되고 cross-platform
/// bit-exact가 성립한다.</para>
///
/// <para><b>규칙</b>(canonical hash schema): <c>GetHashCode</c>/reflection/JSON 순서/Dictionary·HashSet
/// 순회 금지. 문자열은 UTF-8 byte, float는 endianness-독립 IEEE bit pattern, 정수는 명시적 little-endian.
/// units는 <c>UnitId</c> ordinal 오름차순, statuses는 <c>StatusId</c> ordinal 정렬.</para>
/// </summary>
public static class BattleStateCanonicalHash
{
    public const string SchemaVersion = "CanonicalStateHashV1";

    public static string Compute(BattleState state)
    {
        var buffer = new List<byte>(1024);
        WriteString(buffer, SchemaVersion);
        WriteInt(buffer, state.Seed);
        WriteInt(buffer, state.StepIndex);

        // units: stable UnitId 오름차순(ordinal). Allies.Concat(Enemies) 순서나 Dictionary 순회에 의존하지 않는다.
        var units = state.AllUnits
            .OrderBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .ToList();
        WriteInt(buffer, units.Count);
        foreach (var unit in units)
        {
            WriteUnit(buffer, unit);
        }

        return Fnv1a64Hex(buffer);
    }

    private static void WriteUnit(List<byte> buffer, UnitSnapshot unit)
    {
        WriteString(buffer, unit.Id.Value);
        WriteInt(buffer, (int)unit.Side);
        WriteBool(buffer, unit.IsAlive);
        WriteInt(buffer, (int)unit.ActionState);
        WriteFloat(buffer, unit.Position.X);
        WriteFloat(buffer, unit.Position.Y);
        WriteFloat(buffer, unit.CurrentHealth);
        WriteFloat(buffer, unit.CurrentEnergy);
        WriteFloat(buffer, unit.Barrier);
        WriteFloat(buffer, unit.ActionTimerRemaining);
        WriteFloat(buffer, unit.ActionTimerTotal);
        WriteFloat(buffer, unit.CooldownRemaining);
        WriteInt(buffer, unit.PendingActionType.HasValue ? (int)unit.PendingActionType.Value : -1);
        WriteNullableString(buffer, unit.CurrentTargetId?.Value);
        WriteNullableString(buffer, unit.PendingTargetId?.Value);
        WriteNullableString(buffer, unit.PendingSkillId);

        // statuses: StatusId ordinal 정렬(list 적용 순서에 비의존). 동일 StatusId는 Magnitude desc로 안정화.
        var statuses = unit.Statuses
            .OrderBy(status => status.StatusId, StringComparer.Ordinal)
            .ThenByDescending(status => status.Magnitude)
            .ThenByDescending(status => status.RemainingSeconds)
            .ToList();
        WriteInt(buffer, statuses.Count);
        foreach (var status in statuses)
        {
            WriteString(buffer, status.StatusId);
            WriteFloat(buffer, status.RemainingSeconds);
            WriteFloat(buffer, status.Magnitude);
            WriteInt(buffer, status.Stacks);
        }
    }

    // endianness-독립 IEEE-754 bit pattern: GetBytes/ToInt32 round-trip은 같은 platform endian을 써서
    // 논리 bit pattern을 그대로 복원하므로, 이후 WriteInt가 명시적 LE로 직렬화한다.
    private static void WriteFloat(List<byte> buffer, float value)
        => WriteInt(buffer, BitConverter.ToInt32(BitConverter.GetBytes(value), 0));

    private static void WriteInt(List<byte> buffer, int value)
    {
        buffer.Add((byte)(value & 0xFF));
        buffer.Add((byte)((value >> 8) & 0xFF));
        buffer.Add((byte)((value >> 16) & 0xFF));
        buffer.Add((byte)((value >> 24) & 0xFF));
    }

    private static void WriteBool(List<byte> buffer, bool value)
        => buffer.Add(value ? (byte)1 : (byte)0);

    private static void WriteString(List<byte> buffer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        WriteInt(buffer, bytes.Length);
        buffer.AddRange(bytes);
    }

    private static void WriteNullableString(List<byte> buffer, string? value)
    {
        WriteBool(buffer, value != null);
        if (value != null)
        {
            WriteString(buffer, value);
        }
    }

    private static string Fnv1a64Hex(List<byte> bytes)
    {
        unchecked
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offsetBasis;
            foreach (var b in bytes)
            {
                hash ^= b;
                hash *= prime;
            }

            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}
