using Vantah.Core.Cli;
using Xunit;

public class ProcessCmdlineTests
{
    private const string Exe = "/opt/adguardvpn_cli/adguardvpn-cli";

    [Fact]
    public void Matches_process_started_directly_by_its_full_path()
    {
        Assert.True(ProcessCmdline.Matches([Exe, "status"], Exe));
    }

    [Fact]
    public void Matches_privileged_tunnel_hidden_behind_sudo_and_env()
    {
        // Именно так CLI демонизирует туннель: наш бинарь — не нулевой токен.
        string[] cmdline =
        [
            "sudo", "-b", "env", "HOME=/home/user", Exe,
            "connect", "--no-fork", "-l", "Amsterdam",
        ];

        Assert.True(ProcessCmdline.Matches(cmdline, Exe));
    }

    [Fact]
    public void Matches_by_file_name_when_paths_differ()
    {
        Assert.True(ProcessCmdline.Matches(["/usr/local/bin/adguardvpn-cli", "status"], Exe));
    }

    [Fact]
    public void Does_not_match_unrelated_process()
    {
        Assert.False(ProcessCmdline.Matches(["/usr/bin/vantah"], Exe));
    }

    [Fact]
    public void Does_not_match_file_that_merely_mentions_the_binary_name()
    {
        // Путь к логу содержит имя бинаря как подстроку — это не процесс CLI.
        Assert.False(ProcessCmdline.Matches(["tail", "-f", "/var/log/adguardvpn-cli.log"], Exe));
    }

    [Fact]
    public void Empty_cmdline_is_not_a_match()
    {
        Assert.False(ProcessCmdline.Matches([], Exe));
    }
}
