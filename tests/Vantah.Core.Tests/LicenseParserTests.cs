using Vantah.Core.Parsing;
using Xunit;

public class LicenseParserTests
{
    [Fact]
    public void Parses_all_fields()
    {
        var raw =
            "Logged in as [1muser@example.com[0m\n" +
            "You are using the [1mPREMIUM[0m version\n" +
            "Up to [1m10[0m devices simultaneously\n" +
            "Your subscription will be renewed on 2028-07-08\n";
        var lic = LicenseParser.Parse(raw);
        Assert.Equal("user@example.com", lic.Email);
        Assert.Equal("PREMIUM", lic.Plan);
        Assert.Equal(10, lic.MaxDevices);
        Assert.Equal("2028-07-08", lic.RenewalDate);
    }
}
