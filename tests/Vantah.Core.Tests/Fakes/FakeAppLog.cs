using Vantah.Core.Logs;

namespace Vantah.Core.Tests.Fakes;

/// <summary>Лог-обманка: копит строки в списке. Выключенный молчит, как настоящий.</summary>
public sealed class FakeAppLog : IAppLog
{
    public bool Enabled { get; set; } = true;

    public List<string> Lines { get; } = [];

    public void Write(string message)
    {
        if (Enabled) Lines.Add(message);
    }
}
