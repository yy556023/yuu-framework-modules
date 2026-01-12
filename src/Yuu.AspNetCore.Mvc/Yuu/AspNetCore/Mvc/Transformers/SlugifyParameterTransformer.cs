using System;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;

namespace Yuu.AspNetCore.Mvc.Transformers;

public sealed class SlugifyParameterTransformer : IOutboundParameterTransformer
{
    public string? TransformOutbound(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var text = value.ToString();

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return Regex.Replace(
            text,
            "([a-z])([A-Z])",
            "$1-$2",
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100)).ToLowerInvariant();
    }
}
