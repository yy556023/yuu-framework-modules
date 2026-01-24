using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.DependencyInjection;
using Yuu.AspNetCore.Mvc.Transformers;

namespace Yuu.AspNetCore.Mvc.Extensions;

public static class MvcOptionsExtensions
{
    public static void UseSlugifiedRoutes(this MvcOptions options)
    {
        options.Conventions.Add(new RouteTokenTransformerConvention(new PluralizedSlugifyParameterTransformer()));
    }
}
