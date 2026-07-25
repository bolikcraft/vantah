using System.Text.RegularExpressions;
using Vantah.Core.Cli;
using Vantah.Core.Models;

namespace Vantah.Core.Parsing;

public static partial class StatusParser
{
    [GeneratedRegex(@"Connected to (?<loc>.+?) in (?<mode>\S+) mode, running on (?<iface>\S+)",
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
        return new VpnStatus(true,
            m.Groups["loc"].Value.Trim(),
            m.Groups["mode"].Value.Trim(),
            m.Groups["iface"].Value.Trim());
    }
}
