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
    [InlineData("2001:db8::1")]        // голый IPv6-литерал
    [InlineData("::1")]                // сокращённая форма
    [InlineData("[2001:db8::1]:443")]  // в скобках с портом
    [InlineData("[2001:db8::1]")]      // в скобках без порта
    [InlineData("2001:db8::/32")]      // IPv6-CIDR
    public void Normalize_keeps_ipv6_literals(string input)
    {
        Assert.Equal(new[] { input }, DomainNormalizer.Normalize(new[] { input }));
    }

    [Theory]
    [InlineData("--help")]             // флаг: option injection
    [InlineData("-x")]
    [InlineData("-2001:db8::1")]       // «похоже на IPv6», но начинается с «-»
    [InlineData("not a domain")]
    [InlineData("2001:db8::1 --help")] // IPv6 + пробел
    [InlineData("localhost")]          // нет точки и не IPv6
    [InlineData("тест")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(":::")]                // не парсится как адрес
    [InlineData("[2001:db8::1")]       // нет закрывающей скобки
    [InlineData("[2001:db8::1]:notaport")]
    [InlineData("2001:db8::/999")]     // невозможная маска
    public void Normalize_drops_non_domains(string input)
    {
        Assert.Empty(DomainNormalizer.Normalize(new[] { input }));
    }

    [Fact]
    public void Normalize_drops_too_long_ipv6_like_string()
    {
        var tooLong = new string('a', 250) + ".com";
        Assert.Empty(DomainNormalizer.Normalize(new[] { tooLong }));
    }

    [Fact]
    public void Normalize_keeps_253_chars_and_drops_254()
    {
        var ok = new string('a', 249) + ".com";    // ровно 253 — граница включительно
        var tooLong = new string('a', 250) + ".com"; // 254 — уже за границей
        Assert.Equal(253, ok.Length);
        Assert.Equal(254, tooLong.Length);

        var result = DomainNormalizer.Normalize(new[] { ok, tooLong });

        Assert.Equal(new[] { ok }, result);
    }

    [Theory]
    [InlineData("a;b.com")]      // разделитель команд
    [InlineData("evil.com|x")]   // пайп
    [InlineData("evil.com&x")]
    [InlineData("$(id).com")]
    [InlineData("a\tb.com")]     // управляющий символ
    public void Normalize_drops_entries_with_forbidden_characters(string input)
    {
        Assert.Empty(DomainNormalizer.Normalize(new[] { input }));
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
