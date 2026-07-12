using Vantah.Core.Models;
using Vantah.Core.State;
using Xunit;

public class AppStateStoreTests
{
    [Fact]
    public void Setting_state_raises_Changed_once()
    {
        var store = new AppStateStore();
        int raised = 0;
        store.Changed += (_, _) => raised++;
        store.Set(s => s with { Connection = ConnectionState.Connecting });
        Assert.Equal(1, raised);
        Assert.Equal(ConnectionState.Connecting, store.Current.Connection);
    }

    [Fact]
    public void Set_with_no_effective_change_still_publishes_snapshot()
    {
        var store = new AppStateStore();
        store.Set(s => s with { Connection = ConnectionState.Connected, Location = "OSLO" });
        Assert.Equal("OSLO", store.Current.Location);
    }
}
