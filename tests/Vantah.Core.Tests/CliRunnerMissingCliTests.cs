// Vantah — только Linux, поэтому Unix-права доступны всегда (CA1416 предупреждает про Windows).
#pragma warning disable CA1416
using System.ComponentModel;
using Vantah.Core.Cli;
using Vantah.Core.Errors;
using Vantah.Core.Tests.Fakes;

namespace Vantah.Core.Tests;

/// <summary>
/// Без adguardvpn-cli в PATH .NET бросал Win32Exception, и её текст («No such file or
/// directory») уходил в UI как есть. Раннер обязан отдавать свой код ошибки.
/// </summary>
public class CliRunnerMissingCliTests
{
    private static string MissingName() => "vantah-no-such-cli-" + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task Run_without_executable_in_PATH_reports_cli_missing()
    {
        var name = MissingName();

        var ex = await Assert.ThrowsAsync<CliMissingException>(() => new CliRunner(name).RunAsync(["status"]));

        Assert.Equal(new AppError(AppErrorCode.CliMissing, name), ex.Error);
        Assert.Equal(AppErrorCode.CliMissing, AppError.From(ex).Code);
        Assert.IsType<Win32Exception>(ex.InnerException);
    }

    [Fact]
    public async Task Run_with_missing_absolute_path_reports_cli_missing()
    {
        var path = Path.Combine(Path.GetTempPath(), MissingName(), "adguardvpn-cli");

        var ex = await Assert.ThrowsAsync<CliMissingException>(() => new CliRunner(path).RunAsync(["status"]));

        Assert.Equal(new AppError(AppErrorCode.CliMissing, path), ex.Error);
    }

    [Fact]
    public async Task Interactive_start_without_executable_reports_cli_missing()
    {
        var name = MissingName();

        var ex = await Assert.ThrowsAsync<CliMissingException>(() => new CliRunner(name).StartAsync(["login"]));

        Assert.Equal(AppErrorCode.CliMissing, ex.Error.Code);
    }

    /// <summary>
    /// Без CLI опрос падает каждые 4 c. Строка на каждый вызов давала бы ~1800 строк в час и
    /// вытесняла бы из лога всё остальное, поэтому пишем только переход «файл пропал».
    /// </summary>
    [Fact]
    public async Task Missing_executable_is_logged_once_until_it_appears_again()
    {
        var log = new FakeAppLog();
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "adguardvpn-cli");
        var runner = new CliRunner(path, log);
        var missing = $"cli: нет исполняемого файла {path}";

        await Assert.ThrowsAsync<CliMissingException>(() => runner.RunAsync(["status"]));
        await Assert.ThrowsAsync<CliMissingException>(() => runner.RunAsync(["license"]));
        await Assert.ThrowsAsync<CliMissingException>(() => runner.RunAsync(["status"]));
        Assert.Equal([missing], log.Lines);

        // CLI поставили: быстрый успешный опрос в лог не идёт, но счёт начинается заново.
        File.CreateSymbolicLink(path, new[] { "/bin/true", "/usr/bin/true" }.First(File.Exists));
        await runner.RunAsync(["status"]);
        File.Delete(path);
        await Assert.ThrowsAsync<CliMissingException>(() => runner.RunAsync(["status"]));

        Assert.Equal([missing, missing], log.Lines);
    }

    /// <summary>Файл есть, но запустить его нельзя (нет бита x): это не «CLI не установлен».</summary>
    [Fact]
    public async Task Other_start_failures_are_not_reported_as_cli_missing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "adguardvpn-cli");
        await File.WriteAllTextAsync(path, "#!/bin/sh\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        var ex = await Assert.ThrowsAsync<Win32Exception>(() => new CliRunner(path).RunAsync(["status"]));

        Assert.NotEqual(2, ex.NativeErrorCode);
    }
}
