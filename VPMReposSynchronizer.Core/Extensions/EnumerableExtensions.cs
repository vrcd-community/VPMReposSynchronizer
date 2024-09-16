using Microsoft.EntityFrameworkCore;
using VPMReposSynchronizer.Core.Models.Types;

namespace VPMReposSynchronizer.Core.Extensions;

public static class EnumerableExtensions
{
    public static PageResult<TSource> ToPageResult<TSource>(this IEnumerable<TSource> source, int page, int pageSize)
    {
        var items = source.Skip(page * pageSize).Take(pageSize).ToArray();

        return new PageResult<TSource>(items, source.Count());
    }

    public static async ValueTask<PageResult<TSource>> ToPageResultAsync<TSource>(this IQueryable<TSource> source,
        int page, int pageSize)
    {
        var items = await source.Skip(page * pageSize).Take(pageSize).ToArrayAsync();

        return new PageResult<TSource>(items, source.Count());
    }
}
