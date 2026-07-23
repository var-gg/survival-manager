using System;

namespace SM.Combat.Model;

public sealed record BossPressureClockSpec(
    float FirstPulseSeconds,
    float IntervalSeconds,
    float MaxHealthDamageRatio,
    int MaxPulses)
{
    public bool IsEnabled => FirstPulseSeconds > 0f
                             && IntervalSeconds > 0f
                             && MaxHealthDamageRatio > 0f
                             && MaxPulses > 0;

    public bool TryResolvePulse(int stepIndex, float fixedStepSeconds, out int pulseIndex)
    {
        pulseIndex = -1;
        if (!IsEnabled || stepIndex < 0 || fixedStepSeconds <= 0f)
        {
            return false;
        }

        var firstPulseStep = QuantizeSecondsToStep(FirstPulseSeconds, fixedStepSeconds);
        var intervalSteps = QuantizeSecondsToStep(IntervalSeconds, fixedStepSeconds);
        var elapsedSteps = stepIndex - firstPulseStep;
        if (elapsedSteps < 0 || elapsedSteps % intervalSteps != 0)
        {
            return false;
        }

        pulseIndex = elapsedSteps / intervalSteps;
        return pulseIndex < MaxPulses;
    }

    private static int QuantizeSecondsToStep(float seconds, float fixedStepSeconds)
        => Math.Max(1, (int)Math.Ceiling((seconds / fixedStepSeconds) - 0.000001f));
}
