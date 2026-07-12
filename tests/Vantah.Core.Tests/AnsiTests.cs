using Vantah.Core.Cli;
using Xunit;

public class AnsiTests
{
    [Fact]
    public void Strip_removes_color_escapes()
    {
        var input = "Connected to [1mAMSTERDAM[0m in [1mTUN[0m mode";
        Assert.Equal("Connected to AMSTERDAM in TUN mode", Ansi.Strip(input));
    }

    [Fact]
    public void Strip_leaves_plain_text_unchanged()
    {
        Assert.Equal("hello", Ansi.Strip("hello"));
    }
}
