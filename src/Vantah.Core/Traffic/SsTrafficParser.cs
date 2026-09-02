using System.Text.RegularExpressions;

namespace Vantah.Core.Traffic;

/// <summary>Счётчики одного TCP-соединения на момент замера.</summary>
public readonly record struct SocketBytes(string Key, long Sent, long Received);

/// <summary>
/// Разбор вывода <c>ss -tinpa</c>. В режиме SOCKS туннельного интерфейса нет, зато у демона
/// есть долгоживущее соединение с сервером VPN — по нему и считается трафик, как по tun0.
/// Соединения самих клиентов (петля на порт SOCKS) не годятся: короткая закачка успевает
/// открыть и закрыть сокет между опросами, и её байты пропадают вместе с сокетом.
/// </summary>
public static partial class SsTrafficParser
{
    [GeneratedRegex(@"pid=(?<pid>\d+)")]
    private static partial Regex PidRegex();

    [GeneratedRegex(@"bytes_sent:(?<sent>\d+)")]
    private static partial Regex SentRegex();

    [GeneratedRegex(@"bytes_received:(?<received>\d+)")]
    private static partial Regex ReceivedRegex();

    /// <summary>
    /// PID процесса, который слушает порт SOCKS, — это и есть демон. Опознаём демона именно
    /// по слушающему сокету, а не по имени процесса: под тем же именем живут короткие вызовы
    /// CLI (`status` и прочие), и их соединения к API попали бы в счётчик.
    /// </summary>
    public static int? FindDaemonPid(string ssOutput, int socksPort)
    {
        foreach (var line in SocketLines(ssOutput))
        {
            if (!line.StartsWith("LISTEN", StringComparison.Ordinal)) continue;
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (!fields.Any(f => f.EndsWith($":{socksPort}", StringComparison.Ordinal))) continue;
            var pid = PidRegex().Match(line);
            if (pid.Success) return int.Parse(pid.Groups["pid"].Value);
        }
        return null;
    }

    /// <summary>
    /// Соединения демона с внешними адресами — сам туннель. Петлевые пиры отбрасываем: это
    /// клиенты прокси и внутренние сокеты CLI, их байты в туннеле уже посчитаны.
    /// </summary>
    public static IReadOnlyList<SocketBytes> ParseTunnelSockets(string ssOutput, int pid)
    {
        var result = new List<SocketBytes>();
        var lines = ssOutput.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (IsDetailLine(line) || IsHeader(line)) continue;
            if (!line.Contains($"pid={pid},", StringComparison.Ordinal)) continue;

            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 3) continue;
            // Адреса — предпоследние два поля перед колонкой процесса: «локальный» и «пир».
            var processIndex = Array.FindIndex(fields, f => f.StartsWith("users:(", StringComparison.Ordinal));
            if (processIndex < 2) continue;
            var peer = fields[processIndex - 1];
            var local = fields[processIndex - 2];
            if (IsLoopback(peer)) continue;

            // Счётчики лежат в следующей строке (это её и добавляет флаг -i).
            var details = i + 1 < lines.Length ? lines[i + 1] : "";
            if (!IsDetailLine(details)) continue;
            var sent = SentRegex().Match(details);
            var received = ReceivedRegex().Match(details);
            if (!sent.Success || !received.Success) continue;

            result.Add(new SocketBytes(
                $"{local}|{peer}",
                long.Parse(sent.Groups["sent"].Value),
                long.Parse(received.Groups["received"].Value)));
        }
        return result;
    }

    private static IEnumerable<string> SocketLines(string ssOutput) =>
        ssOutput.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => !IsDetailLine(l) && !IsHeader(l));

    // Строку с подробностями ss печатает с отступа — по нему её и отличаем от строки сокета.
    private static bool IsDetailLine(string line) =>
        line.Length > 0 && (line[0] == ' ' || line[0] == '\t');

    private static bool IsHeader(string line) =>
        line.Length == 0
        || line.StartsWith("State", StringComparison.Ordinal)
        || line.StartsWith("Recv-Q", StringComparison.Ordinal)
        || line.StartsWith("Netid", StringComparison.Ordinal);

    private static bool IsLoopback(string address) =>
        address.StartsWith("127.", StringComparison.Ordinal)
        || address.StartsWith("[::1]", StringComparison.Ordinal)
        || address.StartsWith("::1", StringComparison.Ordinal);
}
