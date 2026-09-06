using Vantah.Core.Notifications;
using Xunit;

namespace Vantah.Core.Tests.Notifications;

public class NotificationsStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"), "notifications");

    /// <summary>Смысл фичи — узнать об обрыве, поэтому без файла настройки она включена.</summary>
    [Fact]
    public void Missing_file_means_enabled()
    {
        Assert.True(new NotificationsStore(TempPath()).Load());
    }

    [Fact]
    public void Saved_value_survives_a_reload()
    {
        var path = TempPath();

        new NotificationsStore(path).Save(false);
        Assert.False(new NotificationsStore(path).Load());

        new NotificationsStore(path).Save(true);
        Assert.True(new NotificationsStore(path).Load());
    }

    [Fact]
    public void Broken_file_falls_back_to_enabled()
    {
        var path = TempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "мусор");

        Assert.True(new NotificationsStore(path).Load());
    }
}
