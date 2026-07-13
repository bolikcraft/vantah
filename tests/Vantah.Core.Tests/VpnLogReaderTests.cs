using Vantah.Core.Logs;
using Xunit;

public class VpnLogReaderTests
{
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), $"vantah-log-{Guid.NewGuid():N}.log");

    [Fact]
    public void Missing_file_returns_empty()
    {
        var reader = new VpnLogReader(TempFile());
        Assert.Empty(reader.ReadTail());
    }

    [Fact]
    public void Returns_lines_newest_first_and_filters_noise()
    {
        var path = TempFile();
        try
        {
            File.WriteAllText(path,
                "13.07 INFO CLI_APP CliApp: Start CLI App\n" +   // шум
                "13.07 INFO VPN Connected to AMSTERDAM\n" +
                "13.07 INFO NETWORK_MONITORING get_default_interface: $ ip\n" +  // шум
                "13.07 INFO VPN Tunnel established on tun0\n");

            var tail = new VpnLogReader(path).ReadTail();

            Assert.Equal(2, tail.Count);                       // две шумные строки выброшены
            Assert.Contains("Tunnel established", tail[0]);     // новые сверху
            Assert.Contains("Connected to AMSTERDAM", tail[1]);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void Respects_maxLines()
    {
        var path = TempFile();
        try
        {
            var lines = string.Join('\n', Enumerable.Range(1, 50).Select(i => $"13.07 INFO VPN line {i}"));
            File.WriteAllText(path, lines + "\n");

            var tail = new VpnLogReader(path).ReadTail(maxLines: 10);

            Assert.Equal(10, tail.Count);
            Assert.Contains("line 50", tail[0]);   // самая свежая
            Assert.Contains("line 41", tail[9]);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
