namespace Vantah.Core.Logs;

/// <summary>Свой лог приложения (не лог adguardvpn-cli). Выключенный лог не пишет и не создаёт файл.</summary>
public interface IAppLog
{
    bool Enabled { get; set; }

    void Write(string message);
}

/// <summary>Лог, который всегда молчит — для тестов и мест, где лог не нужен.</summary>
public sealed class NullAppLog : IAppLog
{
    public static readonly NullAppLog Instance = new();

    public bool Enabled { get => false; set { } }

    public void Write(string message) { }
}
