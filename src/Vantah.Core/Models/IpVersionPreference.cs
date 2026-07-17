namespace Vantah.Core.Models;

/// <summary>Какую версию IP форсировать при подключении: обе / только IPv4 / только IPv6.</summary>
public enum IpVersionPreference
{
    Auto,
    IPv4Only,
    IPv6Only,
}
