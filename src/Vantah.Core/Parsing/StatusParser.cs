using System.Text.RegularExpressions;
using Vantah.Core.Cli;
using Vantah.Core.Models;

namespace Vantah.Core.Parsing;

public static partial class StatusParser
{
    // Четыре формы про уже выбранную локацию («Connected to», «Connection lost. Waiting to
    // reconnect to», «Reconnecting to», «Network lost. Waiting to connect to») отличаются только
    // началом, поэтому состояние читаем группой state. Хвост общий и зависит от режима: у туннеля
    // это «running on tun0», у SOCKS — «listening on 127.0.0.1:1080»; хвоста может не быть вовсе.
    [GeneratedRegex(
        @"(?<state>Connected|Waiting to reconnect|Waiting to connect|Reconnecting) to (?<loc>.+?) in (?<mode>\S+) mode(?:, (?:running on (?<iface>\S+)|listening on (?<endpoint>\S+)))?",
        RegexOptions.IgnoreCase)]
    private static partial Regex StatusLineRegex();

    // С флагом --boot (kill switch) connect возвращается мгновенно, и несколько секунд
    // `status` отдаёт «VPN is starting». Без этой ветки переход парсился бы как «отключено».
    [GeneratedRegex(@"VPN is starting", RegexOptions.IgnoreCase)]
    private static partial Regex StartingRegex();

    public static VpnStatus Parse(string cliOutput)
    {
        var text = Ansi.Strip(cliOutput);
        var m = StatusLineRegex().Match(text);
        if (!m.Success)
            return StartingRegex().IsMatch(text) ? VpnStatus.Starting : VpnStatus.Disconnected;
        var iface = m.Groups["iface"];
        var endpoint = m.Groups["endpoint"];
        var location = m.Groups["loc"].Value.Trim();
        var mode = m.Groups["mode"].Value.Trim();
        var ifaceValue = iface.Success ? iface.Value.Trim() : null;
        var endpointValue = endpoint.Success ? endpoint.Value.Trim() : null;

        return m.Groups["state"].Value.Equals("Connected", StringComparison.OrdinalIgnoreCase)
            ? new VpnStatus(true, location, mode, ifaceValue) { Endpoint = endpointValue }
            : VpnStatus.Reconnecting(location, mode, ifaceValue, endpointValue);
    }
}
