namespace SM.Core.Results;

public sealed record Result(bool IsSuccess, string? Error = null)
{
    public static Result Success() => new(true);
    public static Result Fail(string error) => new(false, error);
}
