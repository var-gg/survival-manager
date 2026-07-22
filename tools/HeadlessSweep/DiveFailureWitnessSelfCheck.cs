internal static class DiveFailureWitnessSelfCheck
{
    private const int ExpectedSwitchStep = 15;
    private const double ExpectedSwitchSeconds = 1.5d;

    internal static int Run(string repositoryRoot)
    {
        try
        {
            var observation = CredibleDeckMatchupRunner.RunWitnessRegressionFixture(repositoryRoot);
            var switchObservation = observation.Switch
                ?? throw new InvalidDataException("Expected the known early retarget fixture to contain a switch observation.");
            var losingCandidate = switchObservation.LosingCandidate
                ?? throw new InvalidDataException("Expected the switch journal to contain the losing backline candidate.");

            Require(
                string.Equals(observation.Outcome, DiveFailureBattleObserver.RetargetedAway, StringComparison.Ordinal),
                $"Expected RetargetedAway, observed {observation.Outcome}.");
            Require(switchObservation.StepIndex == ExpectedSwitchStep,
                $"Expected switch step {ExpectedSwitchStep}, observed {switchObservation.StepIndex}.");
            Require(NearlyEqual(switchObservation.ElapsedSeconds, ExpectedSwitchSeconds),
                $"Expected switch time {ExpectedSwitchSeconds}, observed {switchObservation.ElapsedSeconds}.");
            Require(NearlyEqual(observation.ElapsedSeconds, switchObservation.ElapsedSeconds),
                "Witness elapsed time did not come from the observed selector event.");
            Require(NearlyEqual(observation.RemainingDistance, losingCandidate.EdgeDistance),
                "Witness remaining edge distance did not come from the losing candidate at the selector event.");
            Require(NearlyEqual(observation.RemainingCenterPath, losingCandidate.CenterPathDistance),
                "Witness remaining center path did not come from the losing candidate at the selector event.");
            Require(observation.TerminalElapsedSeconds > switchObservation.ElapsedSeconds,
                "Fixture no longer distinguishes the early selector event from terminal battle time.");
            Require(!NearlyEqual(observation.RemainingDistance, observation.TerminalRemainingDistance),
                "Fixture no longer distinguishes selector-event distance from terminal distance.");

            Console.WriteLine(
                "dive-failure-witness-self-check PASS "
                + $"switch_step={switchObservation.StepIndex} "
                + $"switch_seconds={switchObservation.ElapsedSeconds:0.###} "
                + $"event_edge_distance={observation.RemainingDistance:0.######} "
                + $"terminal_seconds={observation.TerminalElapsedSeconds:0.###} "
                + $"terminal_edge_distance={observation.TerminalRemainingDistance:0.######}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"dive-failure-witness-self-check ERROR: {exception}");
            return 2;
        }
    }

    private static bool NearlyEqual(double? left, double? right)
        => left.HasValue
           && right.HasValue
           && Math.Abs(left.Value - right.Value) <= 0.000001d;

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
