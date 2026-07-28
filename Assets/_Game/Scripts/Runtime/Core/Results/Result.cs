namespace SM.Core.Results;

public sealed record Result(
    bool IsSuccess,
    string? Error = null,
    OperationFailure? Failure = null)
{
    public static Result Success() => new(true);
    public static Result Fail(string error) => new(false, error);
    public static Result Fail(OperationFailure failure) => new(false, null, failure);
}
