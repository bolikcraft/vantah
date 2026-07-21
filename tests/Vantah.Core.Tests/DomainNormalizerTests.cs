using Vantah.Core.Exclusions;
using Xunit;

public class DomainNormalizerTests
{
    [Fact]
    public void Normalize_trims_drops_empty_and_dedupes_case_insensitively()
    {
        var input = new[] { "  Example.com ", "", "example.COM", "  ", "foo.net" };
        var result = DomainNormalizer.Normalize(input);
        Assert.Equal(new[] { "Example.com", "foo.net" }, result); // регистр первого вхождения сохранён
    }

    [Fact]
    public void Normalize_drops_entries_that_are_not_domains()
    {
        var result = DomainNormalizer.Normalize(new[]
        {
            "example.com",
            "--help",              // флаг, а не домен
            "-x",                  // флаг
            "not a domain",        // пробелы
            "*.wildcard.org",      // валидный wildcard — остаётся
            "sub.test.co.uk",
            "203.0.113.42",        // IPv4-исключение — остаётся
        });

        Assert.Contains("example.com", result);
        Assert.Contains("*.wildcard.org", result);
        Assert.Contains("sub.test.co.uk", result);
        Assert.Contains("203.0.113.42", result);
        Assert.DoesNotContain("--help", result);
        Assert.DoesNotContain("-x", result);
        Assert.DoesNotContain("not a domain", result);
    }

    [Theory]
    [InlineData("https://www.example.com/path?q=1", "example.com")]
    [InlineData("example.com", "example.com")]
    [InlineData("HTTP://Sub.Example.COM.", "sub.example.com")] // хвостовая точка + lowercase
    [InlineData("www.foo.co.uk", "foo.co.uk")]
    public void ParseUrlToDomain_extracts_host_without_www(string input, string expected)
    {
        Assert.Equal(expected, DomainNormalizer.ParseUrlToDomain(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("localhost")]   // нет точки в имени
    [InlineData("just text")]
    public void ParseUrlToDomain_returns_null_for_non_domains(string input)
    {
        Assert.Null(DomainNormalizer.ParseUrlToDomain(input));
    }

    [Fact]
    public void PasteEntries_produces_www_and_wildcard()
    {
        Assert.Equal(new[] { "www.example.com", "*.example.com" },
            DomainNormalizer.PasteEntries("example.com"));
    }
}
