using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;
using Yuu.EntityFrameworkCore.Logging.Abstractions;

namespace Yuu.EntityFrameworkCore.Logging.Extensions;

public static class YuuEfCoreSqlLoggingExtensions
{
    public static DbContextOptionsBuilder AddEfCoreSqlLoggingInterceptors(this DbContextOptionsBuilder builder, IAbpLazyServiceProvider serviceProvider)
    {
        var dbCommandInterceptor = serviceProvider.GetRequiredService<IToQueryStringCommandInterceptor>();
        builder.AddInterceptors(dbCommandInterceptor);

        return builder;
    }
}
