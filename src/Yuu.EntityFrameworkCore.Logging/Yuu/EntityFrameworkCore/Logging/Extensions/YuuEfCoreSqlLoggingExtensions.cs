using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;
using Yuu.EntityFrameworkCore.Logging.Abstractions;

namespace Yuu.EntityFrameworkCore.Logging.Extensions;

public static class YuuEfCoreSqlLoggingExtensions
{
    public static DbContextOptionsBuilder TryAddEfCoreSqlLoggingInterceptors(this DbContextOptionsBuilder builder, IAbpLazyServiceProvider serviceProvider)
    {
        if (serviceProvider == null)
        {
            return builder;
        }

        var dbCommandInterceptor = serviceProvider.GetRequiredService<IToQueryStringCommandInterceptor>();
        builder.AddInterceptors(dbCommandInterceptor);

        return builder;
    }
}
