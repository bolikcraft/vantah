using System.Linq;
using Vantah.Core.Parsing;
using Xunit;

public class LocationsParserTests
{
    private const string Sample =
        "[1mISO   COUNTRY              CITY                           PING ESTIMATE\n" +
        "[0mEE    Estonia              Tallinn                        24\n" +
        "US    United States        New York                       50\n" +
        "\nYou can connect to a location by running `adguardvpn-cli connect -l '...'`\n";

    [Fact]
    public void Parses_rows_skipping_header_and_footer()
    {
        var list = LocationsParser.Parse(Sample);
        Assert.Equal(2, list.Count);
        Assert.Equal("EE", list[0].IsoCode);
        Assert.Equal("Estonia", list[0].Country);
        Assert.Equal("Tallinn", list[0].City);
        Assert.Equal(24, list[0].PingMs);
        Assert.Equal("United States", list[1].Country);  // страна с пробелом сохраняется
        Assert.Equal("New York", list[1].City);           // город с пробелом сохраняется
    }

    [Fact]
    public void Real_fixture_parses_many_locations()
    {
        var raw = File.ReadAllText("fixtures/list-locations.txt");
        var list = LocationsParser.Parse(raw);
        Assert.True(list.Count > 10);
        Assert.All(list, l => Assert.Equal(2, l.IsoCode.Length));
    }
}
