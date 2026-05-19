using System;
using System.Security.Cryptography;
using System.Text;

namespace SM.Meta.Services;

/// <summary>
/// Pending reward의 RewardCommitId를 deterministic하게 생성한다. caller(SessionRewardSettlementFlow)는
/// 이 id를 SaveProfile mutation의 dedup key로 써서 commit-once를 보장하고, reload 시점에 같은 입력에서
/// 같은 id가 복원되어 pending → committed transition을 idempotent하게 처리할 수 있다.
/// task-reward-settlement-commit-v1 acceptance #1 / #3 / #4의 baseline.
/// </summary>
public static class RewardCommitIdService
{
    /// <summary>
    /// Battle 결과로 만들어진 pending reward의 commit id를 생성한다. battleContextHash가 핵심
    /// discriminator고 outcome(victory/defeat/extract)으로 같은 context의 다른 결과를 분리한다.
    /// </summary>
    public static string Compute(string battleContextHash, string outcome)
    {
        if (string.IsNullOrWhiteSpace(battleContextHash))
        {
            throw new ArgumentException("battleContextHash cannot be empty.", nameof(battleContextHash));
        }

        var normalizedOutcome = string.IsNullOrWhiteSpace(outcome) ? "unknown" : outcome.Trim().ToLowerInvariant();

        using var sha = SHA256.Create();
        var input = $"reward|{battleContextHash}|{normalizedOutcome}";
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var value in bytes)
        {
            builder.Append(value.ToString("x2"));
        }

        return builder.ToString();
    }
}
