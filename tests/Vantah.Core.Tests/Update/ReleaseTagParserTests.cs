using Vantah.Core.Update;
using Xunit;

public class ReleaseTagParserTests
{
    [Theory]
    [InlineData("v0.2.0", "0.2.0")]
    [InlineData("0.2.0", "0.2.0")]
    [InlineData("V1.10.3", "1.10.3")]
    [InlineData(" v2.0.1 ", "2.0.1")]
    [InlineData("v1.2.3-rc1", "1.2.3")]
    [InlineData("v1.2.3+build7", "1.2.3")]
    [InlineData("0.2", "0.2")]
    public void Parses_release_tags(string tag, string expected)
    {
        Assert.Equal(Version.Parse(expected), ReleaseTagParser.Parse(tag));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("latest")]
    [InlineData("v")]
    [InlineData("1")]
    public void Unparsable_tags_are_null(string? tag)
    {
        Assert.Null(ReleaseTagParser.Parse(tag));
    }
}
