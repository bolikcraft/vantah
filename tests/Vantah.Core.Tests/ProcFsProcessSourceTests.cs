using Vantah.Core.Cli;
using Xunit;

/// <summary>
/// Скан procfs на поддельном корне: настоящий /proc нельзя ни подготовить, ни воспроизвести,
/// а формат файлов стабилен (man 5 proc), поэтому источник читает корень из параметра.
/// </summary>
public class ProcFsProcessSourceTests : IDisposable
{
    private const string Exe = "/opt/adguardvpn_cli/adguardvpn-cli";

    /// <summary>1970-01-01 + 1 700 000 000 с — момент загрузки машины в поддельном /proc.</summary>
    private const long BootSeconds = 1_700_000_000;

    private readonly string _root = Directory.CreateTempSubdirectory("vantah-proc-").FullName;

    public ProcFsProcessSourceTests() =>
        File.WriteAllText(Path.Combine(_root, "stat"), $"cpu 1 2 3\nbtime {BootSeconds}\nprocesses 42\n");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <param name="startedAfterBootSec">Сколько секунд после загрузки стартовал процесс.</param>
    private void FakeProcess(int pid, string[] cmdline, string comm = "adguardvpn-cli", int startedAfterBootSec = 0)
    {
        var dir = Directory.CreateDirectory(Path.Combine(_root, pid.ToString())).FullName;
        // cmdline — аргументы, разделённые NUL, с завершающим NUL.
        File.WriteAllText(Path.Combine(dir, "cmdline"), string.Concat(cmdline.Select(a => a + '\0')));

        // stat: pid (comm) state ... starttime — 22-е поле, в тиках (100 Гц). Поля до него не важны,
        // но их количество важно, поэтому забиваем нулями. comm в скобках может содержать пробелы.
        var filler = string.Join(' ', Enumerable.Repeat("0", 18));
        var startTicks = startedAfterBootSec * 100L;
        File.WriteAllText(Path.Combine(dir, "stat"), $"{pid} ({comm}) S {filler} {startTicks} 0 0\n");
    }

    private ProcFsProcessSource Source() => new(Exe, _root);

    [Fact]
    public void Scan_finds_the_privileged_tunnel_with_pid_command_args_and_start_time()
    {
        FakeProcess(1086937, [Exe, "connect", "--no-fork", "-l", "Amsterdam"], startedAfterBootSec: 3600);

        var found = Assert.Single(Source().Scan());

        Assert.Equal(1086937, found.Pid);
        Assert.Equal(1086937, found.Id); // id для UI = pid: реестра больше нет, стабилен сам pid
        Assert.Equal(Exe, found.Command);
        Assert.Equal(["connect", "--no-fork", "-l", "Amsterdam"], found.Args);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(BootSeconds + 3600), found.StartedAt);
    }

    [Fact]
    public void Scan_finds_the_binary_even_when_wrapped_in_sudo()
    {
        FakeProcess(100, ["sudo", "-b", "env", "HOME=/home/user", Exe, "connect"], comm: "sudo");

        var found = Assert.Single(Source().Scan());

        Assert.Equal("sudo", found.Command);
        Assert.Equal("sudo -b env HOME=/home/user " + Exe + " connect", found.CommandLine);
    }

    [Fact]
    public void Scan_skips_foreign_processes()
    {
        FakeProcess(200, ["/usr/bin/vantah"], comm: "vantah");
        FakeProcess(201, ["tail", "-f", "/var/log/adguardvpn-cli.log"], comm: "tail");

        Assert.Empty(Source().Scan());
    }

    [Fact]
    public void Scan_ignores_non_pid_entries_and_processes_that_died_mid_scan()
    {
        Directory.CreateDirectory(Path.Combine(_root, "self"));          // не число
        Directory.CreateDirectory(Path.Combine(_root, "300"));           // pid без cmdline/stat: умер, пока читали
        FakeProcess(301, [Exe, "status"]);

        var found = Assert.Single(Source().Scan());
        Assert.Equal(301, found.Pid);
    }

    [Fact]
    public void Scan_returns_oldest_first()
    {
        FakeProcess(10, [Exe, "status"], startedAfterBootSec: 500);
        FakeProcess(11, [Exe, "connect"], startedAfterBootSec: 100);

        Assert.Equal([11, 10], Source().Scan().Select(p => p.Pid));
    }

    [Fact]
    public void Scan_of_a_missing_root_returns_nothing_instead_of_throwing()
    {
        var source = new ProcFsProcessSource(Exe, Path.Combine(_root, "nope"));

        Assert.Empty(source.Scan());
    }
}
