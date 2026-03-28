namespace SM.Core.Tags;

public readonly record struct Tag(string Value)
{
    public override string ToString() => Value;
}
