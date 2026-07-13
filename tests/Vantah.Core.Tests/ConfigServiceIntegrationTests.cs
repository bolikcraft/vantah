using Vantah.Core.Cli;
using Vantah.Core.Settings;
using Xunit;

/// <summary>
/// Интеграционные: ходят в настоящий adguardvpn-cli. По умолчанию no-op — включаются
/// переменной окружения <c>VANTAH_INTEGRATION=1</c>.
///
/// ТОЛЬКО ЧТЕНИЕ. Записывающих проверок здесь нет намеренно: перезапись настройки даже тем же
/// значением не идемпотентна — CLI теряет пометку «Default (…)» и показывает значение как явно
/// заданное. Прогон тестов не должен молча править конфиг пользователя.
/// Формы `set-*` покрыты юнит-тестами на FakeCliRunner, а сами токены сверены с `config --help`.
/// </summary>
[Trait("Category", "Integration")]
public class ConfigServiceIntegrationTests
{
    private static bool Enabled => Environment.GetEnvironmentVariable("VANTAH_INTEGRATION") == "1";

    /// <summary>Живой вывод `config show` разбирается парсером: ключи и форматы не разъехались.</summary>
    [Fact]
    public async Task Reads_and_parses_the_real_config()
    {
        if (!Enabled) return;

        var svc = new ConfigService(new CliRunner("adguardvpn-cli"));

        var cfg = await svc.GetAsync();

        Assert.InRange(cfg.SocksPort, 1, 65535);
        Assert.False(string.IsNullOrWhiteSpace(cfg.DataDirectory));
        Assert.False(string.IsNullOrWhiteSpace(cfg.DnsUpstream));
    }
}
