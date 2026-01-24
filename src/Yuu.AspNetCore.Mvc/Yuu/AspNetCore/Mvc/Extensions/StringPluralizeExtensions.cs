using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Yuu.AspNetCore.Mvc.Extensions;

public static class StringPluralizeExtensions
{
    public static string Pluralize(this string word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return word;
        }

        var lower = word.ToLowerInvariant();

        var irregulars = new Dictionary<string, string>
        {
            { "person", "people" },
            { "man", "men" },
            { "woman", "women" },
            { "child", "children" },
            { "mouse", "mice" }
        };

        if (irregulars.TryGetValue(lower, out var irregular))
        {
            return MatchCase(word, irregular);
        }

        if (Regex.IsMatch(lower, "(s|x|z|ch|sh)$"))
        {
            return word + "es";
        }

        if (Regex.IsMatch(lower, "[^aeiou]y$"))
        {
            return word[..^1] + "ies";
        }

        if (Regex.IsMatch(lower, "fe$"))
        {
            return word[..^2] + "ves";
        }

        if (Regex.IsMatch(lower, "f$"))
        {
            return word[..^1] + "ves";
        }

        return word + "s";
    }

    private static string MatchCase(string original, string plural)
    {
        if (char.IsUpper(original[0]))
        {
            return char.ToUpper(plural[0]) + plural[1..];
        }

        return plural;
    }
}
