using System;
using System.Collections.Generic;

namespace SM.Core.Results;

public enum OperationFailureKind
{
    Refusal = 0,
    InvariantViolation = 1,
}

public sealed record OperationFailure(
    string Code,
    OperationFailureKind Kind,
    string Diagnostic,
    IReadOnlyList<string> Arguments)
{
    public bool IsInvariantViolation => Kind == OperationFailureKind.InvariantViolation;

    public static OperationFailure Refusal(
        string code,
        string diagnostic,
        params string[] arguments)
        => Create(code, OperationFailureKind.Refusal, diagnostic, arguments);

    public static OperationFailure Invariant(
        string code,
        string diagnostic,
        params string[] arguments)
        => Create(code, OperationFailureKind.InvariantViolation, diagnostic, arguments);

    public override string ToString() => Diagnostic;

    private static OperationFailure Create(
        string code,
        OperationFailureKind kind,
        string diagnostic,
        string[]? arguments)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A stable operation failure code is required.", nameof(code));
        }

        return new OperationFailure(
            code,
            kind,
            diagnostic ?? string.Empty,
            arguments ?? Array.Empty<string>());
    }
}
