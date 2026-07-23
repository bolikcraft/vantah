using Vantah.Core.History;
using Xunit;

public class SessionDurationTests
{
    private const string Hm = "{0}h {1}m";
    private const string M = "{0}m";

    [Theory]
    [InlineData(0, "0m")]           // меньше минуты
    [InlineData(45, "0m")]          // секунды округляются вниз
    [InlineData(5 * 60, "5m")]      // 5 минут
    [InlineData(65 * 60, "1h 5m")]  // час с минутами
    [InlineData(120 * 60, "2h 0m")] // ровно два часа
    public void Formats_span_by_size(int totalSeconds, string expected) =>
        Assert.Equal(expected, SessionDuration.Format(
            System.TimeSpan.FromSeconds(totalSeconds), Hm, M));

    [Fact]
    public void Negative_span_is_treated_as_zero() =>
        Assert.Equal("0m", SessionDuration.Format(
            System.TimeSpan.FromSeconds(-10), Hm, M));
}
