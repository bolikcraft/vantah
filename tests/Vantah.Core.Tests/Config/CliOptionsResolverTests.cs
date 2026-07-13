using Vantah.Core.Config;
using Xunit;

public class CliOptionsResolverTests
{
    private static Func<string, string?> Env(params (string Name, string Value)[] vars)
    {
        var map = vars.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal);
        return name => map.GetValueOrDefault(name);
    }

    private static readonly Func<string, string?> NoEnv = _ => null;

    [Fact]
    public void Executable_defaults_to_adguardvpn_cli()
    {
        var options = CliOptionsResolver.Resolve(IniConfig.Empty, NoEnv);

        Assert.Equal("adguardvpn-cli", options.Executable);
    }

    [Fact]
    public void Executable_comes_from_env_when_config_is_empty()
    {
        var options = CliOptionsResolver.Resolve(
            IniConfig.Empty,
            Env((CliOptionsResolver.ExecutableEnvVar, "/opt/vpn/adguardvpn-cli")));

        Assert.Equal("/opt/vpn/adguardvpn-cli", options.Executable);
    }

    [Fact]
    public void Executable_from_config_wins_over_env()
    {
        var options = CliOptionsResolver.Resolve(
            IniConfig.Parse($"{CliOptionsResolver.ExecutableKey} = /from/config"),
            Env((CliOptionsResolver.ExecutableEnvVar, "/from/env")));

        Assert.Equal("/from/config", options.Executable);
    }

    [Fact]
    public void Whitespace_only_value_falls_through_to_default()
    {
        var options = CliOptionsResolver.Resolve(
            IniConfig.Empty,
            Env((CliOptionsResolver.ExecutableEnvVar, "   ")));

        Assert.Equal("adguardvpn-cli", options.Executable);
    }

    [Fact]
    public void Whitespace_only_value_in_config_falls_through_to_env()
    {
        var options = CliOptionsResolver.Resolve(
            IniConfig.Parse($"{CliOptionsResolver.ExecutableKey} =    "),
            Env((CliOptionsResolver.ExecutableEnvVar, "/from/env")));

        Assert.Equal("/from/env", options.Executable);
    }

    [Fact]
    public void Custom_default_executable_is_respected()
    {
        var options = CliOptionsResolver.Resolve(IniConfig.Empty, NoEnv, "/usr/local/bin/vpn");

        Assert.Equal("/usr/local/bin/vpn", options.Executable);
    }

    [Fact]
    public void KillCommand_is_null_without_config_and_env()
    {
        var options = CliOptionsResolver.Resolve(IniConfig.Empty, NoEnv);

        Assert.Null(options.KillCommand);
    }

    [Fact]
    public void KillCommand_comes_from_env()
    {
        var options = CliOptionsResolver.Resolve(
            IniConfig.Empty,
            Env((CliOptionsResolver.KillCommandEnvVar, "pkexec kill")));

        Assert.Equal("pkexec kill", options.KillCommand);
    }

    [Fact]
    public void KillCommand_from_config_wins_over_env()
    {
        var options = CliOptionsResolver.Resolve(
            IniConfig.Parse($"{CliOptionsResolver.KillCommandKey} = sudo kill -9"),
            Env((CliOptionsResolver.KillCommandEnvVar, "pkexec kill")));

        Assert.Equal("sudo kill -9", options.KillCommand);
    }

    [Fact]
    public void Both_values_resolve_together_from_config()
    {
        var options = CliOptionsResolver.Resolve(
            IniConfig.Parse("""
                # конфиг Vantah
                adguard_cmd = /opt/vpn/adguardvpn-cli
                kill_cmd = pkexec kill
                """),
            NoEnv);

        Assert.Equal(new CliOptions("/opt/vpn/adguardvpn-cli", "pkexec kill"), options);
    }
}
