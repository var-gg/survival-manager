using System;
using System.Linq;

namespace SM.Unity;

public enum SessionTextArgKind
{
    Text = 0,
    Number = 1,
    ItemName = 2,
    AugmentName = 3,
    LocalizedKey = 4,
    Token = 5
}

public sealed record SessionTextArg(
    SessionTextArgKind Kind,
    string TextValue,
    int NumberValue,
    string Table,
    string Key,
    string Fallback)
{
    public SessionTextToken? TokenValue { get; init; }

    public static SessionTextArg Text(string value) => new(SessionTextArgKind.Text, value ?? string.Empty, 0, string.Empty, string.Empty, string.Empty);

    public static SessionTextArg Number(int value) => new(SessionTextArgKind.Number, string.Empty, value, string.Empty, string.Empty, string.Empty);

    public static SessionTextArg ItemName(string itemId) => new(SessionTextArgKind.ItemName, itemId ?? string.Empty, 0, string.Empty, string.Empty, string.Empty);

    public static SessionTextArg AugmentName(string augmentId) => new(SessionTextArgKind.AugmentName, augmentId ?? string.Empty, 0, string.Empty, string.Empty, string.Empty);

    public static SessionTextArg Localized(string table, string key, string fallback) => new(SessionTextArgKind.LocalizedKey, string.Empty, 0, table ?? string.Empty, key ?? string.Empty, fallback ?? string.Empty);

    public static SessionTextArg Token(SessionTextToken token) => new(SessionTextArgKind.Token, string.Empty, 0, string.Empty, string.Empty, string.Empty)
    {
        TokenValue = token
    };

    public object Resolve(GameLocalizationController? localization, ContentTextResolver? contentText)
    {
        return Kind switch
        {
            SessionTextArgKind.Number => NumberValue,
            SessionTextArgKind.ItemName => contentText?.GetItemName(TextValue) ?? TextValue,
            SessionTextArgKind.AugmentName => contentText?.GetAugmentName(TextValue) ?? TextValue,
            SessionTextArgKind.LocalizedKey => localization != null
                ? localization.LocalizeOrFallback(Table, Key, Fallback)
                : Fallback,
            SessionTextArgKind.Token => TokenValue?.Resolve(localization, contentText) ?? string.Empty,
            _ => TextValue,
        };
    }
}

public sealed record SessionTextToken(
    string Table,
    string Key,
    string Fallback,
    params SessionTextArg[] Arguments)
{
    public static SessionTextToken Empty { get; } = new(string.Empty, string.Empty, string.Empty);

    public static SessionTextToken Plain(string text) => new(string.Empty, string.Empty, text ?? string.Empty);

    public bool HasValue => !string.IsNullOrWhiteSpace(Key) || !string.IsNullOrWhiteSpace(Fallback);

    public string Resolve(GameLocalizationController? localization, ContentTextResolver? contentText = null)
    {
        if (!HasValue)
        {
            return string.Empty;
        }

        var resolvedArguments = Arguments.Select(argument => argument.Resolve(localization, contentText)).ToArray();
        if (localization != null && !string.IsNullOrWhiteSpace(Table) && !string.IsNullOrWhiteSpace(Key))
        {
            return localization.LocalizeOrFallback(Table, Key, Fallback, resolvedArguments);
        }

        if (resolvedArguments.Length == 0)
        {
            return Fallback;
        }

        try
        {
            return string.Format(Fallback, resolvedArguments);
        }
        catch (FormatException)
        {
            return Fallback;
        }
    }
}
