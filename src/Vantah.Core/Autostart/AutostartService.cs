namespace Vantah.Core.Autostart;

/// <summary>Автозапуск Vantah при входе в систему через freedesktop-autostart
/// (~/.config/autostart/vantah.desktop). Источник правды — наличие файла.</summary>
public sealed class AutostartService
{
    private readonly string _file;
    private readonly string _execCommand;
    private readonly string _iconName;

    public AutostartService(string autostartDir, string execCommand, string iconName)
    {
        _file = Path.Combine(autostartDir, "vantah.desktop");
        _execCommand = execCommand;
        _iconName = iconName;
    }

    public bool IsEnabled() => File.Exists(_file);

    public void Enable()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        var content =
            "[Desktop Entry]\n" +
            "Type=Application\n" +
            "Name=Vantah\n" +
            $"Exec={QuoteExec(_execCommand)}\n" +
            $"Icon={_iconName}\n" +
            "Terminal=false\n" +
            "StartupWMClass=Vantah.App\n" +
            "X-GNOME-Autostart-enabled=true\n";
        File.WriteAllText(_file, content);
    }

    public void Disable()
    {
        if (File.Exists(_file)) File.Delete(_file);
    }

    private static string QuoteExec(string path)
    {
        // Экранирование по freedesktop desktop-entry spec (ключ Exec, значение в двойных кавычках):
        // спецсимволы " \ $ ` предваряются обратным слэшем. Порядок важен: \ первым.
        var escaped = path
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("$", "\\$")
            .Replace("`", "\\`");
        return $"\"{escaped}\"";
    }
}
