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

    [Theory]
    [InlineData(new object[] { new[] { "vim", "adguardvpn-cli" } })]          // редактируем файл с таким именем
    [InlineData(new object[] { new[] { "tail", "-f", "adguardvpn-cli" } })]
    // «-c» отдаёт целую команду одним токеном: его basename — тоже «adguardvpn-cli», но это не наш процесс.
    [InlineData(new object[] { new[] { "bash", "-c", "vim /tmp/adguardvpn-cli" } })]
    public void Editor_opening_a_file_named_like_cli_is_not_a_cli_process(string[] cmdline)
        => Assert.False(ProcessCmdline.Matches(cmdline, Exe));

    [Fact]
    public void Wrapper_without_our_binary_is_not_a_match()
    {
        Assert.False(ProcessCmdline.Matches(["sudo", "-b", "env", "tail", "/var/log/x"], Exe));
    }

    [Theory]
    [InlineData(new object[] { new[] { "adguardvpn-cli", "connect" } })]                      // прямой запуск
    [InlineData(new object[] { new[] { "sudo", "-b", "env", "adguardvpn-cli", "connect" } })] // обёртка привилегий
    [InlineData(new object[] { new[] { "/usr/local/bin/adguardvpn-cli", "status" } })]
    public void Real_cli_invocations_match(string[] cmdline)
        => Assert.True(ProcessCmdline.Matches(cmdline, Exe));

    [Fact]
    public void Empty_cmdline_is_not_a_match()
    {
        Assert.False(ProcessCmdline.Matches([], Exe));
    }
}
