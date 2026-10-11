using System.Runtime.CompilerServices;
using Puntjes.Sdk.Generated.Models;

namespace Puntjes.Sdk;

/// <summary>Walks a paginated call page by page, starting at page 1, until the last page in its meta.</summary>
public static class Paginator
{
    public static async IAsyncEnumerable<TItem> AllAsync<TItem>(
        Func<int, CancellationToken, Task<(IEnumerable<TItem>? Items, PaginationMeta? Meta)>> fetch,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var page = 1; ; page++)
        {
            var (items, meta) = await fetch(page, cancellationToken);
            if (page > 1 && meta?.CurrentPage != page)
            {
                yield break;
            }

            foreach (var item in items ?? [])
            {
                yield return item;
            }

            if (meta?.CurrentPage != page || page >= meta.LastPage)
            {
                yield break;
            }
        }
    }
}
