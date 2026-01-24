using Microsoft.AspNetCore.Routing;
using Yuu.AspNetCore.Mvc.Extensions;

namespace Yuu.AspNetCore.Mvc.Transformers;

public sealed class PluralizedSlugifyParameterTransformer : IOutboundParameterTransformer
{
    private readonly SlugifyParameterTransformer _slugifyTransformer;

    public PluralizedSlugifyParameterTransformer()
    {
        _slugifyTransformer = new SlugifyParameterTransformer();
    }

    public string? TransformOutbound(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var text = value.ToString();

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var pluralized = text.Pluralize();

        return _slugifyTransformer.TransformOutbound(pluralized);
    }
}
