using SM.Core.Results;
using UnityEngine;

namespace SM.Unity;

internal static class SessionOperationFailureBoundary
{
    internal static Result RefuseSessionOperation(
        string code,
        string diagnostic,
        params string[] arguments)
        => Result.Fail(new OperationFailure(
            code,
            OperationFailureKind.Refusal,
            string.Empty,
            arguments));

    internal static Result FailSessionInvariant(
        string scope,
        string code,
        string diagnostic,
        params string[] arguments)
    {
        Debug.LogError($"[{scope}] cause='{code}' diagnostic='{diagnostic}'");
        return Result.Fail(new OperationFailure(
            code,
            OperationFailureKind.InvariantViolation,
            string.Empty,
            arguments));
    }

    internal static Result ForwardSessionFailure(
        string scope,
        OperationFailure? failure,
        string diagnosticWhenMissing)
    {
        var resolved = failure
                       ?? OperationFailure.Invariant(
                           SessionOperationFailureCodes.GenericOperationFailed,
                           diagnosticWhenMissing);
        if (resolved.IsInvariantViolation)
        {
            Debug.LogError(
                $"[{scope}] cause='{resolved.Code}' diagnostic='{resolved.Diagnostic}'");
        }

        return Result.Fail(new OperationFailure(
            resolved.Code,
            resolved.Kind,
            string.Empty,
            resolved.Arguments));
    }
}
