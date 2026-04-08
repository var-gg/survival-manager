using SM.Combat.Model;

namespace SM.Unity;

internal static class RuntimeOperationalTelemetry
{
    internal static TelemetryEventRecord CreateCheckpointStarted(
        string runId,
        SessionCheckpointKind kind,
        string profileId)
    {
        return CreateRecord(TelemetryEventKind.CheckpointStarted, runId, kind, profileId);
    }

    internal static TelemetryEventRecord CreateCheckpointSucceeded(
        string runId,
        SessionCheckpointKind kind,
        string profileId,
        SessionCheckpointStatus status,
        int payloadBytes,
        string message)
    {
        return CreateRecord(
            status == SessionCheckpointStatus.RecoveredFromBackup
                ? TelemetryEventKind.CheckpointRecoveredFromBackup
                : TelemetryEventKind.CheckpointSucceeded,
            runId,
            kind,
            profileId,
            payloadBytes,
            message,
            usedBackup: status == SessionCheckpointStatus.RecoveredFromBackup);
    }

    internal static TelemetryEventRecord CreateCheckpointFailed(
        string runId,
        SessionCheckpointKind kind,
        string profileId,
        string message,
        string quarantinePath)
    {
        return CreateRecord(
            string.IsNullOrWhiteSpace(quarantinePath)
                ? TelemetryEventKind.CheckpointFailed
                : TelemetryEventKind.CorruptSaveQuarantined,
            runId,
            kind,
            profileId,
            0f,
            string.IsNullOrWhiteSpace(quarantinePath) ? message : quarantinePath,
            usedBackup: !string.IsNullOrWhiteSpace(quarantinePath),
            stringValueB: message);
    }

    internal static TelemetryEventRecord CreateManualReloadBlocked(string runId, string profileId, string reason)
    {
        return CreateRecord(
            TelemetryEventKind.ManualReloadBlocked,
            runId,
            SessionCheckpointKind.ManualLoad,
            profileId,
            0f,
            reason);
    }

    internal static TelemetryEventRecord CreateSmokeRestoreFromDisk(string runId, string profileId, string message)
    {
        return CreateRecord(
            TelemetryEventKind.SmokeRestoreFromDisk,
            runId,
            SessionCheckpointKind.QuickBattleRestore,
            profileId,
            0f,
            message);
    }

    internal static TelemetryEventRecord CreateRewardSettlementResumed(string runId, string sourceId)
    {
        return CreateOperational(
            TelemetryEventKind.RewardSettlementResumed,
            runId,
            sourceId,
            string.Empty,
            0f,
            false);
    }

    internal static TelemetryEventRecord CreateRewardSettlementDuplicatePrevented(string runId, string sourceId)
    {
        return CreateOperational(
            TelemetryEventKind.RewardSettlementDuplicatePrevented,
            runId,
            sourceId,
            string.Empty,
            0f,
            true);
    }

    internal static TelemetryEventRecord CreateRewardOptionsPresented(
        string runId,
        string sourceId,
        int optionCount,
        int gold,
        int echo)
    {
        return CreateOperational(
            TelemetryEventKind.RewardOptionsPresented,
            runId,
            sourceId,
            $"{gold}|{echo}",
            optionCount,
            false);
    }

    internal static TelemetryEventRecord CreateRewardOptionChosen(
        string runId,
        string sourceId,
        string chosenOptionId,
        int gold,
        int echo)
    {
        return CreateOperational(
            TelemetryEventKind.RewardOptionChosen,
            runId,
            sourceId,
            chosenOptionId,
            gold,
            false,
            echo);
    }

    internal static TelemetryEventRecord CreateEconomySnapshot(
        string runId,
        string label,
        int gold,
        int echo,
        int pendingRewardCount,
        bool defeatRecoveryWindow)
    {
        return new TelemetryEventRecord
        {
            Domain = TelemetryDomain.Operational,
            EventKind = TelemetryEventKind.EconomySnapshot,
            TimeSeconds = 0f,
            Actor = new TelemetryEntityRef(),
            Target = new TelemetryEntityRef(),
            Explain = CreateExplainStamp("economy_snapshot"),
            StringValueA = label,
            StringValueB = pendingRewardCount.ToString(),
            ValueA = gold,
            ValueB = echo,
            IntValueA = pendingRewardCount,
            BoolValueA = defeatRecoveryWindow,
        };
    }

    private static TelemetryEventRecord CreateCheckpointRecord(
        TelemetryEventKind kind,
        string runId,
        SessionCheckpointKind checkpointKind,
        string profileId,
        float valueA,
        string stringValueA,
        bool usedBackup,
        string stringValueB = "")
    {
        return new TelemetryEventRecord
        {
            Domain = TelemetryDomain.Operational,
            EventKind = kind,
            TimeSeconds = 0f,
            Actor = new TelemetryEntityRef(),
            Target = new TelemetryEntityRef(),
            Explain = CreateExplainStamp("checkpoint"),
            StringValueA = checkpointKind.ToString(),
            StringValueB = string.IsNullOrWhiteSpace(stringValueB) ? $"{profileId}|{runId}" : stringValueB,
            ValueA = valueA,
            BoolValueA = usedBackup,
            IntValueA = (int)checkpointKind,
        };
    }

    private static TelemetryEventRecord CreateRecord(
        TelemetryEventKind kind,
        string runId,
        SessionCheckpointKind checkpointKind,
        string profileId,
        float valueA = 0f,
        string stringValueA = "",
        bool usedBackup = false,
        string stringValueB = "")
    {
        return CreateCheckpointRecord(kind, runId, checkpointKind, profileId, valueA, stringValueA, usedBackup, stringValueB);
    }

    private static TelemetryEventRecord CreateOperational(
        TelemetryEventKind kind,
        string runId,
        string stringValueA,
        string stringValueB,
        float valueA,
        bool boolValueA,
        float valueB = 0f)
    {
        return new TelemetryEventRecord
        {
            Domain = TelemetryDomain.Operational,
            EventKind = kind,
            TimeSeconds = 0f,
            Actor = new TelemetryEntityRef(),
            Target = new TelemetryEntityRef(),
            Explain = CreateExplainStamp("runtime_operational"),
            StringValueA = stringValueA,
            StringValueB = string.IsNullOrWhiteSpace(stringValueB) ? runId : stringValueB,
            ValueA = valueA,
            ValueB = valueB,
            BoolValueA = boolValueA,
        };
    }

    private static ExplainStamp CreateExplainStamp(string sourceContentId)
    {
        return new ExplainStamp
        {
            SourceKind = ExplainedSourceKind.SystemRule,
            SourceContentId = sourceContentId,
            SourceDisplayName = "Runtime Operational",
            ReasonCode = DecisionReasonCode.RunEconomyChoice,
            Salience = SalienceClass.Minor,
        };
    }
}
