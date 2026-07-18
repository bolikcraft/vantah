using Vantah.Core.Auth;
using Xunit;

namespace Vantah.Core.Tests;

public class DeviceCodeParserTests
{
    [Fact]
    public void Parses_url_code_and_seconds_from_fixture()
    {
        var raw = File.ReadAllText("fixtures/login-device-code.txt");
        var prompt = DeviceCodeParser.Parse(raw);
        Assert.NotNull(prompt);
        Assert.Equal("https://2tbkh3igt.btoe0irlwwby.lol/device_code?user_code=FKGB-NNBQ", prompt!.Url);
        Assert.Equal("FKGB-NNBQ", prompt.UserCode);
        Assert.Equal(1673, prompt.ExpiresInSeconds);
    }

    [Fact]
    public void Returns_null_when_no_link_yet()
    {
        Assert.Null(DeviceCodeParser.Parse("Starting login...\n"));
        Assert.Null(DeviceCodeParser.Parse(""));
    }

    [Fact]
    public void Strips_ansi_before_matching()
    {
        var prompt = DeviceCodeParser.Parse(
            "\x1b[1mThe following link\x1b[0m: https://example.test/device_code?user_code=AAAA-BBBB");
        Assert.NotNull(prompt);
        Assert.Equal("https://example.test/device_code?user_code=AAAA-BBBB", prompt!.Url);
        Assert.Equal("AAAA-BBBB", prompt.UserCode);
    }

    [Fact]
    public void Url_without_user_code_still_parses()
    {
        var prompt = DeviceCodeParser.Parse("Open https://example.test/auth to continue");
        Assert.NotNull(prompt);
        Assert.Equal("https://example.test/auth", prompt!.Url);
        Assert.Null(prompt.UserCode);
    }
}
