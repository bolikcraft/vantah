using Vantah.Core.Config;

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
            $"Exec={QuoteExec(StripNewlines(_execCommand))}\n" +
            $"Icon={StripNewlines(_iconName)}\n" +
            "Terminal=false\n" +
            "StartupWMClass=Vantah.App\n" +
            "X-GNOME-Autostart-enabled=true\n";
        // Атомарно (временный файл → rename), иначе обрыв записи оставил бы битый автозапуск.
        // secureDirectory: false — ~/.config/autostart общий каталог freedesktop, куда пишут все
        // приложения и который читает сессионный менеджер; поджимать его до 700 нельзя.
        SecureFile.WriteAllText(_file, content, secureDirectory: false);
    }

    public void Disable()
    {
        if (File.Exists(_file)) File.Delete(_file);
    }

    // Значение ключа Desktop Entry — ровно одна строка: перевод строки внутри значения
    // вырвался бы из неё и внедрил произвольные ключи (второй Exec=, Hidden= и т.п.).
    private static string StripNewlines(string s) => s.Replace("\r", "").Replace("\n", "");

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
