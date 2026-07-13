using Vantah.Core.Models;

namespace Vantah.Core.Locations;

public static class LocationSorter
{
    /// <summary>
    /// Сортирует локации по колонке с учётом направления. Строковые колонки — без учёта регистра,
    /// пинг — числом. При favoritesFirst признак «в избранном» становится первичным ключом:
    /// избранные всплывают наверх, внутри групп сохраняется порядок по колонке (OrderBy стабилен).
    /// Вход не мутируется.
    /// </summary>
    public static IReadOnlyList<Location> Sort(
        IReadOnlyList<Location> items,
        LocationSortKey key,
        bool ascending,
        bool favoritesFirst,
        IReadOnlySet<string> favoriteKeys)
    {
        var cmp = StringComparer.OrdinalIgnoreCase;

        IEnumerable<Location> sorted = key switch
        {
            LocationSortKey.Iso => ascending
                ? items.OrderBy(l => l.IsoCode, cmp)
                : items.OrderByDescending(l => l.IsoCode, cmp),
            LocationSortKey.Country => ascending
                ? items.OrderBy(l => l.Country, cmp)
                : items.OrderByDescending(l => l.Country, cmp),
            LocationSortKey.City => ascending
                ? items.OrderBy(l => l.City, cmp)
                : items.OrderByDescending(l => l.City, cmp),
            LocationSortKey.Ping => ascending
                ? items.OrderBy(l => l.PingMs)
                : items.OrderByDescending(l => l.PingMs),
            _ => items
        };

        if (favoritesFirst)
            sorted = sorted.ToList().OrderBy(l => favoriteKeys.Contains(l.Key) ? 0 : 1);

        return sorted.ToList();
    }
}
