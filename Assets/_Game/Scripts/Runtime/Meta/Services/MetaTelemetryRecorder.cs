using System;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Meta.Model;

namespace SM.Meta.Services;

public static class MetaTelemetryRecorder
{
    public static TelemetryEventRecord CreateRecruitPackGenerated(
        string runId,
        int seed,
        int offerCount,
        RecruitPhaseState phaseState)
    {
        return CreateRecord(
            TelemetryDomain.Recruit,
            TelemetryEventKind.RecruitPackGenerated,
            runId,
            seed,
            offerCount,
            phaseState.PendingScoutDirective?.Kind.ToString() ?? string.Empty,
            phaseState.ScoutUsedThisPhase);
    }

    public static TelemetryEventRecord CreateRecruitPurchased(
        string runId,
        RecruitUnitPreview offer,
        int offerIndex)
    {
        return CreateRecord(
            TelemetryDomain.Recruit,
            TelemetryEventKind.RecruitPurchased,
            runId,
            offer.Metadata.GoldCost,
            offerIndex,
            offer.UnitBlueprintId,
            false,
            BuildActor(offer.UnitBlueprintId, offer.UnitInstanceSeed, false));
    }

    public static TelemetryEventRecord CreateRecruitRefreshed(string runId, int goldSpent, int refreshCount)
    {
        return CreateRecord(
            TelemetryDomain.Recruit,
            TelemetryEventKind.RecruitRefreshed,
            runId,
            goldSpent,
            refreshCount,
            string.Empty,
            false);
    }

    public static TelemetryEventRecord CreateScoutUsed(string runId, ScoutDirective directive, int echoCost)
    {
        return CreateRecord(
            TelemetryDomain.Recruit,
            TelemetryEventKind.ScoutUsed,
            runId,
            echoCost,
            (int)directive.Kind,
            directive.SynergyTagId ?? string.Empty,
            true);
    }

    public static TelemetryEventRecord CreateRetrainPerformed(
        string runId,
        string heroId,
        string archetypeId,
        RetrainOperationKind operation,
        RetrainResult result)
    {
        return CreateRecord(
            TelemetryDomain.Retrain,
            TelemetryEventKind.RetrainPerformed,
            runId,
            result.EchoCost,
            (int)operation,
            $"{result.FlexActiveId}|{result.FlexPassiveId}",
            false,
            BuildActor(archetypeId, heroId, false));
    }

    public static TelemetryEventRecord CreateDuplicateConverted(
        string runId,
        RecruitUnitPreview offer,
        DuplicateConversionResult duplicate)
    {
        return CreateRecord(
            TelemetryDomain.Duplicate,
            TelemetryEventKind.DuplicateConverted,
            runId,
            duplicate.EchoGranted,
            0,
            offer.UnitBlueprintId,
            false,
            BuildActor(offer.UnitBlueprintId, offer.UnitInstanceSeed, false));
    }

    private static TelemetryEventRecord CreateRecord(
        TelemetryDomain domain,
        TelemetryEventKind kind,
        string runId,
        float valueA,
        int intValueA,
        string stringValueA,
        bool boolValueA,
        TelemetryEntityRef? actor = null)
    {
        return new TelemetryEventRecord
        {
            Domain = domain,
            EventKind = kind,
            TimeSeconds = 0f,
            Actor = actor ?? new TelemetryEntityRef(),
            Target = new TelemetryEntityRef(),
            Explain = new ExplainStamp
            {
                SourceKind = ExplainedSourceKind.SystemRule,
                SourceContentId = "loop_d_meta_runtime",
                SourceDisplayName = "Meta Runtime",
                ReasonCode = DecisionReasonCode.RunEconomyChoice,
                Salience = SalienceClass.Minor,
            },
            ValueA = valueA,
            IntValueA = intValueA,
            StringValueA = stringValueA,
            BoolValueA = boolValueA,
            StringValueB = runId ?? string.Empty,
        };
    }

    private static TelemetryEntityRef BuildActor(string blueprintId, string unitInstanceId, bool isSummon)
    {
        return new TelemetryEntityRef
        {
            UnitBlueprintId = blueprintId ?? string.Empty,
            UnitInstanceId = unitInstanceId ?? string.Empty,
            OwnerUnitInstanceId = string.Empty,
            IsSummon = isSummon,
            IsDeployable = false,
            SideIndex = 0,
        };
    }
}
