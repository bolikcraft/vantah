namespace Vantah.Core.Models;

public sealed record Location(string IsoCode, string Country, string City, int PingMs)
{
    public string Key => $"{IsoCode}|{City}";
}
