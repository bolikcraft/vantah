using Vantah.Core.Config;
using Xunit;

public class IniConfigTests
{
    [Fact]
    public void Parse_reads_key_value_pairs()
    {
        var config = IniConfig.Parse("adguard_cmd = /opt/adguard/adguardvpn-cli\nkill_cmd = pkexec kill");

        Assert.Equal("/opt/adguard/adguardvpn-cli", config.Get("adguard_cmd"));
        Assert.Equal("pkexec kill", config.Get("kill_cmd"));
    }

    [Fact]
    public void Parse_trims_spaces_around_key_and_value()
    {
        var config = IniConfig.Parse("   adguard_cmd   =    /usr/bin/vpn   ");

        Assert.Equal("/usr/bin/vpn", config.Get("adguard_cmd"));
    }

    [Fact]
    public void Get_is_case_insensitive()
    {
        var config = IniConfig.Parse("Adguard_Cmd = /usr/bin/vpn");

        Assert.Equal("/usr/bin/vpn", config.Get("ADGUARD_CMD"));
    }

    [Fact]
    public void Parse_ignores_comments_blank_lines_and_sections()
    {
        var config = IniConfig.Parse("""
            # решётка — комментарий
            ; точка с запятой — тоже

            [section]
            adguard_cmd = /usr/bin/vpn
            """);

        Assert.Equal("/usr/bin/vpn", config.Get("adguard_cmd"));
        Assert.Null(config.Get("section"));
    }

    [Fact]
    public void Get_returns_null_for_missing_and_empty_values()
    {
        var config = IniConfig.Parse("kill_cmd =");

        Assert.Null(config.Get("kill_cmd"));
        Assert.Null(config.Get("adguard_cmd"));
    }

    [Fact]
    public void Parse_ignores_leading_byte_order_mark()
    {
        var config = IniConfig.Parse("﻿adguard_cmd = /usr/bin/vpn");

        Assert.Equal("/usr/bin/vpn", config.Get("adguard_cmd"));
    }

    [Fact]
    public void Load_unreadable_file_returns_empty_config()
    {
        // Файл есть, но прочитать его нельзя — эксклюзивный захват. Конфиг обязан промолчать,
        // а не уронить старт приложения.
        var path = Path.Combine(Path.GetTempPath(), $"vantah-ini-{Guid.NewGuid():N}.conf");
        File.WriteAllText(path, "adguard_cmd = /tmp/fake-cli\n");
        try
        {
            using var _ = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            Assert.Null(IniConfig.Load(path).Get("adguard_cmd"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_missing_file_returns_empty_config()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vantah-ini-{Guid.NewGuid():N}.conf");

        Assert.Null(IniConfig.Load(path).Get("adguard_cmd"));
    }

    [Fact]
    public void Load_reads_existing_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vantah-ini-{Guid.NewGuid():N}.conf");
        File.WriteAllText(path, "# конфиг\nadguard_cmd = /tmp/fake-cli\n");
        try
        {
            Assert.Equal("/tmp/fake-cli", IniConfig.Load(path).Get("adguard_cmd"));
        }
        finally { File.Delete(path); }
    }
}
