using System.Text.RegularExpressions;

namespace SM.Content.Definitions
{

    public static class LocalizationKeyPattern
    {
        private static readonly Regex KeyPattern = new(
            "^[a-z0-9]+(?:\\.[a-z0-9_]+)+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool IsValid(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && KeyPattern.IsMatch(key);
        }
    }
}
