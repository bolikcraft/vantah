using System.Text.RegularExpressions;
using Vantah.Core.Cli;
using Vantah.Core.Models;

namespace Vantah.Core.Parsing;

public static partial class StatusParser
{
    // Хвост строки зависит от режима: у туннеля это «running on tun0», у SOCKS —
    // «listening on 127.0.0.1:1080». Поэтому подключение опознаём по началу строки, а хвост
    // читаем как необязательный: интерфейс есть только у туннеля, адрес прокси — только у SOCKS.
    [GeneratedRegex(
        @"Connected to (?<loc>.+?) in (?<mode>\S+) mode(?:, (?:running on (?<iface>\S+)|listening on (?<endpoint>\S+)))?",
        RegexOptions.IgnoreCase)]
    private static partial Regex ConnectedRegex();

    // С флагом --boot (kill switch) connect возвращается мгновенно, и несколько секунд
    // `status` отдаёт «VPN is starting». Без этой ветки переход парсился бы как «отключено».
    [GeneratedRegex(@"VPN is starting", RegexOptions.IgnoreCase)]
    private static partial Regex StartingRegex();

    public static VpnStatus Parse(string cliOutput)
    {
        var text = Ansi.Strip(cliOutput);
        var m = ConnectedRegex().Match(text);
        if (!m.Success)
            return StartingRegex().IsMatch(text) ? VpnStatus.Starting : VpnStatus.Disconnected;
        var iface = m.Groups["iface"];
        var endpoint = m.Groups["endpoint"];
        return new VpnStatus(true,
            m.Groups["loc"].Value.Trim(),
            m.Groups["mode"].Value.Trim(),
            iface.Success ? iface.Value.Trim() : null)
        {
            Endpoint = endpoint.Success ? endpoint.Value.Trim() : null,
        };
    }
}
