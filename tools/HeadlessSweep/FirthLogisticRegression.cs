using System.Globalization;

internal sealed record FirthClearObservation(
    string SquadId,
    int SeedSalt,
    int Heat,
    bool Won);

internal sealed record FirthFitResult(
    bool Converged,
    int Iterations,
    IReadOnlyList<EndlessHeatCompositionGamma> GammaByComposition,
    double MaxGammaSpread);

/// <summary>
/// Small, dependency-free Firth logistic fit for the endless Heat measurement grid. Seed salts are
/// fixed categorical effects; composition intercepts and composition-by-Heat interactions use
/// frontline as the reference level. Reported gammas are centered total Heat slopes, so their spread
/// is invariant to the reference coding.
/// </summary>
internal static class FirthLogisticRegression
{
    private static readonly string[] SquadOrder = { "frontline", "mixed", "ranged" };
    private const int MaximumIterations = 200;
    private const double ConvergenceTolerance = 1e-9d;
    private const double ProbabilityFloor = 1e-12d;

    internal static FirthFitResult Fit(IReadOnlyList<FirthClearObservation> observations)
    {
        if (observations.Count == 0)
        {
            throw new ArgumentException("Firth fit requires observations.", nameof(observations));
        }

        var seeds = observations.Select(value => value.SeedSalt).Distinct().OrderBy(value => value).ToArray();
        var seedColumn = seeds.Skip(1)
            .Select((seed, index) => (seed, column: 6 + index))
            .ToDictionary(value => value.seed, value => value.column);
        var parameterCount = 6 + Math.Max(0, seeds.Length - 1);
        var design = new double[observations.Count][];
        var outcomes = new double[observations.Count];
        for (var row = 0; row < observations.Count; row++)
        {
            var observation = observations[row];
            var x = new double[parameterCount];
            x[0] = 1d;
            x[1] = string.Equals(observation.SquadId, "mixed", StringComparison.Ordinal) ? 1d : 0d;
            x[2] = string.Equals(observation.SquadId, "ranged", StringComparison.Ordinal) ? 1d : 0d;
            x[3] = observation.Heat;
            x[4] = x[1] * observation.Heat;
            x[5] = x[2] * observation.Heat;
            if (seedColumn.TryGetValue(observation.SeedSalt, out var column))
            {
                x[column] = 1d;
            }

            design[row] = x;
            outcomes[row] = observation.Won ? 1d : 0d;
        }

        var beta = new double[parameterCount];
        var converged = false;
        var iterations = 0;
        for (var iteration = 1; iteration <= MaximumIterations; iteration++)
        {
            iterations = iteration;
            var state = Evaluate(design, outcomes, beta);
            var adjustedScore = new double[parameterCount];
            for (var row = 0; row < design.Length; row++)
            {
                var x = design[row];
                var leverage = state.Weights[row] * QuadraticForm(x, state.InformationInverse);
                var adjustedResidual = outcomes[row]
                                       - state.Probabilities[row]
                                       + (leverage * (0.5d - state.Probabilities[row]));
                for (var column = 0; column < parameterCount; column++)
                {
                    adjustedScore[column] += x[column] * adjustedResidual;
                }
            }

            var direction = SolveCholesky(state.InformationCholesky, adjustedScore);
            var maxDirection = direction.Max(value => Math.Abs(value));
            if (maxDirection > 5d)
            {
                var scale = 5d / maxDirection;
                for (var column = 0; column < direction.Length; column++)
                {
                    direction[column] *= scale;
                }
            }

            var step = 1d;
            var accepted = false;
            var candidate = new double[parameterCount];
            while (step >= 1d / 1024d)
            {
                for (var column = 0; column < parameterCount; column++)
                {
                    candidate[column] = beta[column] + (direction[column] * step);
                }

                var candidateObjective = PenalizedLogLikelihood(design, outcomes, candidate);
                if (candidateObjective + 1e-10d >= state.PenalizedLogLikelihood)
                {
                    accepted = true;
                    break;
                }

                step *= 0.5d;
            }

            if (!accepted)
            {
                break;
            }

            var maxAppliedStep = 0d;
            for (var column = 0; column < parameterCount; column++)
            {
                maxAppliedStep = Math.Max(
                    maxAppliedStep,
                    Math.Abs(candidate[column] - beta[column]));
                beta[column] = candidate[column];
            }

            if (maxAppliedStep < ConvergenceTolerance)
            {
                converged = true;
                break;
            }
        }

        var totalSlopes = new[]
        {
            beta[3],
            beta[3] + beta[4],
            beta[3] + beta[5],
        };
        var meanSlope = totalSlopes.Average();
        var gammas = SquadOrder
            .Select((squadId, index) => new EndlessHeatCompositionGamma(
                squadId,
                totalSlopes[index] - meanSlope))
            .ToArray();
        return new FirthFitResult(
            converged,
            iterations,
            gammas,
            totalSlopes.Max() - totalSlopes.Min());
    }

    private static FitState Evaluate(
        IReadOnlyList<double[]> design,
        IReadOnlyList<double> outcomes,
        IReadOnlyList<double> beta)
    {
        var probabilities = new double[design.Count];
        var weights = new double[design.Count];
        var information = new double[beta.Count, beta.Count];
        var logLikelihood = 0d;
        for (var row = 0; row < design.Count; row++)
        {
            var eta = Dot(design[row], beta);
            var probability = Logistic(eta);
            probabilities[row] = probability;
            weights[row] = Math.Max(ProbabilityFloor, probability * (1d - probability));
            logLikelihood += outcomes[row] * Math.Log(Math.Max(ProbabilityFloor, probability))
                             + ((1d - outcomes[row])
                                * Math.Log(Math.Max(ProbabilityFloor, 1d - probability)));
            var x = design[row];
            for (var left = 0; left < beta.Count; left++)
            {
                for (var right = 0; right <= left; right++)
                {
                    information[left, right] += weights[row] * x[left] * x[right];
                }
            }
        }

        for (var left = 0; left < beta.Count; left++)
        {
            for (var right = 0; right < left; right++)
            {
                information[right, left] = information[left, right];
            }
        }

        var cholesky = Cholesky(information);
        var inverse = InvertFromCholesky(cholesky);
        var logDeterminant = 0d;
        for (var index = 0; index < beta.Count; index++)
        {
            logDeterminant += 2d * Math.Log(cholesky[index, index]);
        }

        return new FitState(
            probabilities,
            weights,
            cholesky,
            inverse,
            logLikelihood + (0.5d * logDeterminant));
    }

    private static double PenalizedLogLikelihood(
        IReadOnlyList<double[]> design,
        IReadOnlyList<double> outcomes,
        IReadOnlyList<double> beta)
        => Evaluate(design, outcomes, beta).PenalizedLogLikelihood;

    private static double[,] Cholesky(double[,] matrix)
    {
        var size = matrix.GetLength(0);
        var result = new double[size, size];
        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column <= row; column++)
            {
                var sum = matrix[row, column];
                for (var k = 0; k < column; k++)
                {
                    sum -= result[row, k] * result[column, k];
                }

                if (row == column)
                {
                    if (sum <= 1e-12d || !double.IsFinite(sum))
                    {
                        throw new InvalidOperationException(
                            "Firth information matrix is not positive definite at column "
                            + row.ToString(CultureInfo.InvariantCulture)
                            + ".");
                    }

                    result[row, column] = Math.Sqrt(sum);
                }
                else
                {
                    result[row, column] = sum / result[column, column];
                }
            }
        }

        return result;
    }

    private static double[] SolveCholesky(double[,] cholesky, IReadOnlyList<double> rightHandSide)
    {
        var size = rightHandSide.Count;
        var forward = new double[size];
        for (var row = 0; row < size; row++)
        {
            var value = rightHandSide[row];
            for (var column = 0; column < row; column++)
            {
                value -= cholesky[row, column] * forward[column];
            }

            forward[row] = value / cholesky[row, row];
        }

        var solution = new double[size];
        for (var row = size - 1; row >= 0; row--)
        {
            var value = forward[row];
            for (var column = row + 1; column < size; column++)
            {
                value -= cholesky[column, row] * solution[column];
            }

            solution[row] = value / cholesky[row, row];
        }

        return solution;
    }

    private static double[,] InvertFromCholesky(double[,] cholesky)
    {
        var size = cholesky.GetLength(0);
        var inverse = new double[size, size];
        for (var column = 0; column < size; column++)
        {
            var basis = new double[size];
            basis[column] = 1d;
            var solution = SolveCholesky(cholesky, basis);
            for (var row = 0; row < size; row++)
            {
                inverse[row, column] = solution[row];
            }
        }

        return inverse;
    }

    private static double QuadraticForm(IReadOnlyList<double> vector, double[,] matrix)
    {
        var sum = 0d;
        for (var row = 0; row < vector.Count; row++)
        {
            var projected = 0d;
            for (var column = 0; column < vector.Count; column++)
            {
                projected += matrix[row, column] * vector[column];
            }

            sum += vector[row] * projected;
        }

        return sum;
    }

    private static double Dot(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        var sum = 0d;
        for (var index = 0; index < left.Count; index++)
        {
            sum += left[index] * right[index];
        }

        return sum;
    }

    private static double Logistic(double value)
    {
        if (value >= 0d)
        {
            var exp = Math.Exp(-Math.Min(40d, value));
            return 1d / (1d + exp);
        }

        var negativeExp = Math.Exp(Math.Max(-40d, value));
        return negativeExp / (1d + negativeExp);
    }

    private sealed record FitState(
        IReadOnlyList<double> Probabilities,
        IReadOnlyList<double> Weights,
        double[,] InformationCholesky,
        double[,] InformationInverse,
        double PenalizedLogLikelihood);
}
