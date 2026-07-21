using Avalonia.Headless.XUnit;
using Vantah.App.Localization;
using Vantah.App.ViewModels;
using Vantah.Core.Exclusions;
using Vantah.Core.Models;
using Vantah.Core.State;

/// <summary>
/// Ручной ввод домена проходит ту же проверку <see cref="DomainNormalizer.IsAcceptableDomain"/>,
/// что импорт файла и парс вывода CLI: опечатка («exmaple», «not a domain») не должна молча
/// уезжать в adguardvpn-cli и возвращаться невнятной ошибкой или мусорной записью в списке.
/// </summary>
public class DomainsViewModelAddValidationTests
{
    private sealed class RecordingExclusions(ExclusionsSnapshot snapshot) : IExclusionsService
    {
        public List<string> Added { get; } = new();

        public async Task<ExclusionsSnapshot> GetAsync(CancellationToken ct = default)
        {
            await Task.Yield();
            return snapshot;
        }

        public Task AddAsync(string domain, CancellationToken ct = default)
        {
            Added.Add(domain);
            return Task.CompletedTask;
        }

        public Exception? RemoveThrows { get; set; }

        public Task RemoveAsync(string domain, CancellationToken ct = default) =>
            RemoveThrows is null ? Task.CompletedTask : Task.FromException(RemoveThrows);

        public Task SetModeAsync(SiteExclusionMode from, SiteExclusionMode to,
            IReadOnlyList<string> currentDomains, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static (DomainsViewModel Vm, RecordingExclusions Service) MakeVm(string[]? domains = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vantah-tests", Guid.NewGuid().ToString("N"));
        var store = new ExclusionsStore(Path.Combine(dir, "site-exclusions"));
        var service = new RecordingExclusions(
            new ExclusionsSnapshot(SiteExclusionMode.General, domains ?? []));
        return (new DomainsViewModel(service, store, new AppStateStore()), service);
    }

    [AvaloniaTheory]
    [InlineData("not a domain")]
    [InlineData("--help")]
    [InlineData("exmaple")]
    public async Task Invalid_entry_is_not_sent_to_cli_and_sets_error(string input)
    {
        var (vm, service) = MakeVm();
        await vm.LoadTask;

        vm.Query = input;
        await vm.AddCommand.ExecuteAsync(null);

        Assert.Empty(service.Added);
        Assert.Equal(Localizer.Instance[LocKeys.Domains_InvalidEntry], vm.Error);
        Assert.Equal(input, vm.Query);   // ввод не затёрт — есть что поправить
    }

    [AvaloniaFact]
    public async Task Valid_domain_is_added()
    {
        var (vm, service) = MakeVm();
        await vm.LoadTask;

        vm.Query = "example.com";
        await vm.AddCommand.ExecuteAsync(null);

        Assert.Equal(["example.com"], service.Added);
        Assert.Null(vm.Error);
        Assert.Equal("", vm.Query);
    }

    [AvaloniaFact]
    public async Task Ipv6_literal_is_added()
    {
        var (vm, service) = MakeVm();
        await vm.LoadTask;

        vm.Query = "2001:db8::1";
        await vm.AddCommand.ExecuteAsync(null);

        Assert.Equal(["2001:db8::1"], service.Added);
        Assert.Null(vm.Error);
    }

    [AvaloniaFact]
    public async Task Error_disappears_as_soon_as_the_entry_is_corrected()
    {
        var (vm, _) = MakeVm();
        await vm.LoadTask;

        vm.Query = "exmaple";
        await vm.AddCommand.ExecuteAsync(null);
        Assert.NotNull(vm.Error);

        vm.Query = "example.com";   // пользователь правит опечатку — жалоба больше не актуальна

        Assert.Null(vm.Error);
    }

    [AvaloniaFact]
    public async Task Error_from_a_failed_removal_survives_typing_in_the_filter()
    {
        var (vm, service) = MakeVm(["example.com"]);
        await vm.LoadTask;
        service.RemoveThrows = new InvalidOperationException("CLI недоступен");

        await vm.RemoveCommand.ExecuteAsync(vm.Items[0]);
        Assert.Equal("CLI недоступен", vm.Error);

        vm.Query = "exa";   // поле — ещё и фильтр списка: поиск строки не должен глушить жалобу CLI

        Assert.Equal("CLI недоступен", vm.Error);
    }

    [AvaloniaFact]
    public async Task Empty_input_neither_calls_cli_nor_sets_error()
    {
        var (vm, service) = MakeVm();
        await vm.LoadTask;

        vm.Query = "   ";
        await vm.AddCommand.ExecuteAsync(null);

        Assert.Empty(service.Added);
        Assert.Null(vm.Error);
    }
}
