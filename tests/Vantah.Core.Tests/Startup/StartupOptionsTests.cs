using Vantah.Core.Startup;
using Xunit;

namespace Vantah.Core.Tests.Startup;

public class StartupOptionsTests
{
    [Fact]
    public void No_arguments_means_a_normal_start()
    {
        var o = StartupOptions.Parse([]);

        Assert.False(o.Help);
        Assert.False(o.Version);
        Assert.False(o.Minimized);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public void Help_is_recognized(string arg)
    {
        Assert.True(StartupOptions.Parse([arg]).Help);
    }

    [Fact]
    public void Version_is_recognized()
    {
        Assert.True(StartupOptions.Parse(["--version"]).Version);
    }

    [Fact]
    public void Minimized_is_recognized()
    {
        Assert.True(StartupOptions.Parse([StartupOptions.MinimizedFlag]).Minimized);
    }

    // В .desktop-файлы и в автозапуск сессии попадает всякое — падать из-за этого нельзя.
    [Fact]
    public void Unknown_arguments_are_ignored()
    {
        var o = StartupOptions.Parse(["--frobnicate", "-x", "", "/tmp/file", "--minimize"]);

        Assert.False(o.Help);
        Assert.False(o.Version);
        Assert.False(o.Minimized);
    }

    [Fact]
    public void Known_arguments_survive_unknown_neighbours()
    {
        var o = StartupOptions.Parse(["--frobnicate", StartupOptions.MinimizedFlag, "--nope"]);

        Assert.True(o.Minimized);
    }

    [Fact]
    public void Flags_combine()
    {
        var o = StartupOptions.Parse(["--version", "--help", StartupOptions.MinimizedFlag]);

        Assert.True(o.Help);
        Assert.True(o.Version);
        Assert.True(o.Minimized);
    }

    [Fact]
    public void Help_text_lists_every_flag()
    {
        Assert.Contains("--minimized", StartupOptions.HelpText);
        Assert.Contains("--version", StartupOptions.HelpText);
        Assert.Contains("--help", StartupOptions.HelpText);
    }
}
