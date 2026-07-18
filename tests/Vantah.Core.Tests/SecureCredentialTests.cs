using Vantah.Core.Auth;
using Xunit;

namespace Vantah.Core.Tests;

public class SecureCredentialTests
{
    [Fact]
    public void Exposes_chars_until_cleared()
    {
        using var c = new SecureCredential("p4ss".ToCharArray());
        Assert.Equal("p4ss", new string(c.Reveal()));
    }

    [Fact]
    public void ToString_is_redacted()
    {
        using var c = new SecureCredential("secret".ToCharArray());
        Assert.Equal("***", c.ToString());
    }

    [Fact]
    public void Clear_zeroes_backing_array()
    {
        var raw = "secret".ToCharArray();
        var c = new SecureCredential(raw);
        c.Clear();
        Assert.All(raw, ch => Assert.Equal('\0', ch));   // исходный буфер занулён
    }

    [Fact]
    public void Reveal_after_clear_throws()
    {
        var c = new SecureCredential("x".ToCharArray());
        c.Clear();
        Assert.Throws<ObjectDisposedException>(() => c.Reveal());
    }

    [Fact]
    public void IsEmpty_reflects_state()
    {
        var c = new SecureCredential("x".ToCharArray());
        Assert.False(c.IsEmpty);
        c.Clear();
        Assert.True(c.IsEmpty);
    }
}
