using Vantah.Core.Exclusions;
using Vantah.Core.Models;
using Xunit;

// Реальная фикстура (E1-0): заголовок `Exclusions for \x1b[1mGENERAL\x1b[0m mode:`
// (режим обёрнут ANSI-жирным), далее домены по одному в строке.
public class ExclusionsParserTests
{
    [Fact]
    public void Parses_general_mode_and_domains_stripping_ansi()
    {
        var raw =
            "Exclusions for \x1b[1mGENERAL\x1b[0m mode:\n" +
            "example.com\n" +
            "*.foo.net\n" +
            "\n";
        var snap = ExclusionsParser.Parse(raw);
        Assert.Equal(SiteExclusionMode.General, snap.Mode);
        Assert.Equal(new[] { "example.com", "*.foo.net" }, snap.Domains);
    }

    [Fact]
    public void Parses_selective_mode_header()
    {
        var raw = "Exclusions for SELECTIVE mode:\nbank.example\n";
        var snap = ExclusionsParser.Parse(raw);
        Assert.Equal(SiteExclusionMode.Selective, snap.Mode);
        Assert.Single(snap.Domains);
        Assert.Equal("bank.example", snap.Domains[0]);
    }

    [Fact]
    public void Empty_list_yields_mode_and_no_domains()
    {
        var snap = ExclusionsParser.Parse("Exclusions for GENERAL mode:\n");
        Assert.Empty(snap.Domains);
    }

    [Fact]
    public void Domains_are_normalized_and_deduped()
    {
        var raw = "Exclusions for GENERAL mode:\nExample.com\nexample.COM\n";
        Assert.Single(ExclusionsParser.Parse(raw).Domains); // dedupe без регистра
    }

    [Fact]
    public void Real_fixture_is_recognized()
    {
        var raw = File.ReadAllText("fixtures/site-exclusions-general.txt");
        var snap = ExclusionsParser.Parse(raw);
        Assert.Equal(SiteExclusionMode.General, snap.Mode);
        Assert.NotEmpty(snap.Domains);
        // Строки из фикстуры (E1-0): домен без маски и IP-исключение.
        Assert.Contains("plain.example", snap.Domains);
        Assert.Contains("203.0.113.42", snap.Domains);
    }

    [Fact]
    public void Non_domain_noise_lines_are_ignored()
    {
        var raw = "Exclusions for general mode:\n" +
                  "example.com\n" +
                  "No exclusions configured\n" +
                  "Type a domain to add\n" +
                  "sub.test.org\n";
        var domains = ExclusionsParser.Parse(raw).Domains;
        Assert.Contains("example.com", domains);
        Assert.Contains("sub.test.org", domains);
        Assert.DoesNotContain("No exclusions configured", domains);
        Assert.DoesNotContain("Type a domain to add", domains);
    }
}
