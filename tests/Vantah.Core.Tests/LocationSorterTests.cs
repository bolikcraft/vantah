using Vantah.Core.Locations;
using Vantah.Core.Models;
using Xunit;

public class LocationSorterTests
{
    private static IReadOnlyList<Location> Sample() => new[]
    {
        new Location("US", "United States", "New York", 50),
        new Location("EE", "Estonia",       "Tallinn",  24),
        new Location("DE", "Germany",       "Berlin",   30),
        new Location("de", "germany",       "aachen",   30)
    };

    private static readonly IReadOnlySet<string> NoFavorites = new HashSet<string>();

    [Fact]
    public void Ping_ascending_sorts_by_number()
    {
        var sorted = LocationSorter.Sort(Sample(), LocationSortKey.Ping, ascending: true, favoritesFirst: false, NoFavorites);

        Assert.Equal(new[] { 24, 30, 30, 50 }, sorted.Select(l => l.PingMs));
        Assert.Equal("Tallinn", sorted[0].City);
    }

    [Fact]
    public void Ping_descending_reverses_order()
    {
        var sorted = LocationSorter.Sort(Sample(), LocationSortKey.Ping, ascending: false, favoritesFirst: false, NoFavorites);

        Assert.Equal(new[] { 50, 30, 30, 24 }, sorted.Select(l => l.PingMs));
        Assert.Equal("New York", sorted[0].City);
        Assert.Equal("Tallinn", sorted[^1].City);
    }

    [Fact]
    public void Country_ascending_ignores_case()
    {
        var sorted = LocationSorter.Sort(Sample(), LocationSortKey.Country, ascending: true, favoritesFirst: false, NoFavorites);

        Assert.Equal(new[] { "Estonia", "Germany", "germany", "United States" }, sorted.Select(l => l.Country));
    }

    [Fact]
    public void City_descending_ignores_case()
    {
        var sorted = LocationSorter.Sort(Sample(), LocationSortKey.City, ascending: false, favoritesFirst: false, NoFavorites);

        Assert.Equal(new[] { "Tallinn", "New York", "Berlin", "aachen" }, sorted.Select(l => l.City));
    }

    [Fact]
    public void Iso_ascending_ignores_case()
    {
        var sorted = LocationSorter.Sort(Sample(), LocationSortKey.Iso, ascending: true, favoritesFirst: false, NoFavorites);

        Assert.Equal(new[] { "DE", "de", "EE", "US" }, sorted.Select(l => l.IsoCode));
    }

    [Fact]
    public void Favorites_first_lifts_favorites_and_keeps_inner_order()
    {
        var favorites = new HashSet<string> { "US|New York" };

        var sorted = LocationSorter.Sort(Sample(), LocationSortKey.Ping, ascending: true, favoritesFirst: true, favorites);

        Assert.Equal("New York", sorted[0].City);
        Assert.Equal(new[] { 24, 30, 30 }, sorted.Skip(1).Select(l => l.PingMs));
    }

    [Fact]
    public void Favorites_first_disabled_ignores_favorites()
    {
        var favorites = new HashSet<string> { "US|New York" };

        var sorted = LocationSorter.Sort(Sample(), LocationSortKey.Ping, ascending: true, favoritesFirst: false, favorites);

        Assert.Equal("Tallinn", sorted[0].City);
    }

    [Fact]
    public void Sort_does_not_mutate_input()
    {
        var items = Sample();
        var before = items.ToArray();

        LocationSorter.Sort(items, LocationSortKey.Ping, ascending: true, favoritesFirst: true, new HashSet<string> { "US|New York" });

        Assert.Equal(before, items);
    }
}
