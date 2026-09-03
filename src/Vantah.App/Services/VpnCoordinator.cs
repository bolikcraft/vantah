using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vantah.Core.Auth;
using Vantah.Core.History;
using Vantah.Core.Logs;
using Vantah.Core.Models;
using Vantah.Core.Errors;
using Vantah.Core.State;
using Vantah.Core.Traffic;
using Vantah.Core.Vpn;

namespace Vantah.App.Services;

public sealed class VpnCoordinator(
    IVpnService vpn,
    TrafficMonitor traffic,
    AppStateStore store,
    ConnectionHistoryTracker history,
    IpVersionStore ipVersionStore,
    IAuthService auth,
    LastLocationStore? lastLocation = null,
    KillSwitchStore? killSwitch = null,
    IAppLog? log = null,
    TimeSpan? loginProbePeriod = null)
{
    private readonly IAppLog _log = log ?? NullAppLog.Instance;

    private DateTime _lastPollUtc = DateTime.UtcNow;
    // _opGate сериализует операции; _operationInFlight гейтит опрос — самодостаточен,
    // не завязан на счётчик семафора.
    private readonly SemaphoreSlim _opGate = new(1, 1);
    private volatile bool _operationInFlight;
    private int _pollInFlight;
    // Пока демон восстанавливает связь, `status` подвисает и упирается в таймаут CLI:
    // одиночный промах не должен показывать «Ошибка» вместо живого туннеля.
    private const int PollFailuresBeforeError = 2;
    private int _pollFailures;
    private readonly TimeSpan _loginProbePeriod = loginProbePeriod ?? TimeSpan.FromSeconds(30);
    private DateTime _lastLoginProbeUtc = DateTime.UtcNow;
    private int _loginProbeInFlight;
    private volatile IReadOnlyList<Location> _knownLocations = Array.Empty<Location>();

    /// <summary>Список известных локаций для обогащения истории Country/Ping (город → страна/пинг).</summary>
    public void UpdateKnownLocations(IReadOnlyList<Location> locations) => _knownLocations = locations;

    /// <summary>Завершённые сессии для UI (newest-first, cap 12).</summary>
    public IReadOnlyList<ConnectionHistoryEntry> PreviousConnections => history.Previous;

    /// <summary>Зонд состояния логина (через `license`). Обновляет снапшот, чтобы UI гейтил
    /// форму входа. Ошибки глотаем: CLI/сеть недоступны — не роняем UI, оставляем прежнее.</summary>
    public async Task RefreshLoginStateAsync(CancellationToken ct = default)
    {
        try
        {
            var state = await auth.GetLoginStateAsync(ct);
            store.Set(s => s with { LoginState = state });
        }
        catch { /* оставляем прежнее состояние */ }
    }

    /// <summary>Последний фоновый зонд логина — чтобы его можно было дождаться в тестах.</summary>
    public Task LoginProbeTask { get; private set; } = Task.CompletedTask;

    // Зонд логина в фоне: он не должен задерживать опрос статуса. Раз в полминуты — вход
    // меняется редко, а команда каждый раз ходит в сеть.
    private void ProbeLoginInBackground(CancellationToken ct)
    {
        if (DateTime.UtcNow - _lastLoginProbeUtc < _loginProbePeriod) return;
        if (Interlocked.Exchange(ref _loginProbeInFlight, 1) == 1) return;
        _lastLoginProbeUtc = DateTime.UtcNow;
        LoginProbeTask = Task.Run(async () =>
        {
            try { await RefreshLoginStateAsync(ct); }
            finally { Interlocked.Exchange(ref _loginProbeInFlight, 0); }
        }, ct);
    }

    public async Task PollOnceAsync(CancellationToken ct = default)
    {
        // Во время connect/disconnect (CLI может работать долго) опрос
        // не должен перетереть состояние обратно в Disconnected.
        if (_operationInFlight) return;

        // Single-flight: не даём двум опросам перекрыться (напр. таймер тикнул раньше,
        // чем предыдущий опрос успел завершиться).
        if (Interlocked.Exchange(ref _pollInFlight, 1) == 1) return;
        try
        {
            // Пока состояние логина неизвестно, ждём его: от него зависят гейт формы входа и план
            // автоподключения на старте. Дальше только поддерживаем — `license` ходит в сеть и при
            // обрыве висит до таймаута 15 c, а статус за это время успевает смениться дважды.
            if (store.Current.LoginState == LoginState.Unknown)
                await RefreshLoginStateAsync(ct);
            else
                ProbeLoginInBackground(ct);

            try
            {
                var status = await vpn.GetStatusAsync(ct);
                _pollFailures = 0;
                var now = DateTime.UtcNow;
                var elapsed = (now - _lastPollUtc).TotalSeconds;
                _lastPollUtc = now;

                TrafficSample? sample = null;
                if (status.IsConnected && status.Interface is { } iface)
                    sample = traffic.Poll(iface, elapsed);
                // В режиме SOCKS интерфейса нет: считаем по соединениям демона, а найти его
                // помогает порт прокси из той же строки статуса.
                else if (status.IsConnected && status.SocksPort is { } socksPort)
                    sample = traffic.PollSocks(socksPort, elapsed);
                else
                    traffic.Reset();

                if (status.Phase is VpnPhase.Starting or VpnPhase.Reconnecting)
                {
                    // Туннель поднимается либо kill switch его восстанавливает: показываем
                    // «Подключение», но не трогаем историю — бесконечные ретраи иначе плодили бы
                    // ложные разрывы сессий. У Starting локация и режим пусты, поэтому известные
                    // значения сохраняем; у Reconnecting они есть — их и пишем.
                    var known = status.Phase == VpnPhase.Reconnecting;
                    SetState(s => s with
                    {
                        Connection = ConnectionState.Connecting,
                        Location = known ? status.Location : s.Location,
                        LocationDisplay = known ? ResolveLocationDisplay(status) : s.LocationDisplay,
                        Mode = known ? status.Mode : s.Mode,
                        Interface = known ? status.Interface : s.Interface,
                        Traffic = null,
                        Error = null,
                    }, "опрос");
                    return;
                }

                TrackHistory(status);

                SetState(s => s with
                {
                    Connection = status.IsConnected ? ConnectionState.Connected : ConnectionState.Disconnected,
                    Location = status.Location,
                    LocationDisplay = ResolveLocationDisplay(status),
                    Mode = status.Mode,
                    Interface = status.Interface,
                    Traffic = sample,
                    Error = null,
                }, "опрос");
            }
            catch (Exception ex)
            {
                // Первый сбой подряд глотаем: снапшот остаётся прежним, решение принимаем
                // по второму промаху.
                var reason = OneLine(ex.Message);
                if (++_pollFailures >= PollFailuresBeforeError)
                {
                    _log.Write($"опрос не удался ({_pollFailures}/{PollFailuresBeforeError}): {reason}");
                    SetState(s => s with { Connection = ConnectionState.Error, Error = AppError.From(ex) }, "опрос");
                }
                else
                {
                    _log.Write($"опрос не удался ({_pollFailures}/{PollFailuresBeforeError}),"
                        + $" состояние оставлено прежним: {reason}");
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _pollInFlight, 0);
        }
    }

    public async Task ConnectAsync(string? location, bool fastest, CancellationToken ct = default)
    {
        // Семафор сериализует connect/disconnect: два быстрых клика не запустят
        // две параллельные операции CLI, второй вызов дождётся завершения первого.
        await _opGate.WaitAsync(ct);
        try
        {
            _operationInFlight = true;
            SetState(s => s with { Connection = ConnectionState.Connecting, Error = null }, "connect");
            try
            {
                var status = await vpn.ConnectAsync(
                    location, fastest, ipVersionStore.Load(), killSwitch?.Load() ?? false, ct);

                if (status.Phase is VpnPhase.Starting or VpnPhase.Reconnecting)
                {
                    // С kill switch (--boot) CLI возвращается раньше, чем поднят туннель, а после
                    // обрыва отвечает «Reconnecting»: писать Disconnected нельзя — на «Статусе»
                    // мигало бы «Отключено», а история закрывала бы только что открытую сессию.
                    // Реальное состояние допишет опрос.
                    try { if (!string.IsNullOrWhiteSpace(location)) lastLocation?.Save(location); }
                    catch { /* best-effort persist, не роняем состояние подключения */ }
                    var known = status.Phase == VpnPhase.Reconnecting;
                    SetState(s => s with
                    {
                        Connection = ConnectionState.Connecting,
                        Location = known ? status.Location : s.Location,
                        LocationDisplay = known ? ResolveLocationDisplay(status) : s.LocationDisplay,
                        Mode = known ? status.Mode : s.Mode,
                        Interface = known ? status.Interface : s.Interface,
                        Traffic = known ? null : s.Traffic,
                        Error = null,
                    }, "connect");
                    return;
                }

                TrackHistory(status);
                if (status.IsConnected)
                {
                    try { lastLocation?.Save(location ?? status.Location); }
                    catch { /* best-effort persist, не роняем состояние подключения */ }
                }
                SetState(s => s with
                {
                    Connection = status.IsConnected ? ConnectionState.Connected : ConnectionState.Disconnected,
                    Location = status.Location, LocationDisplay = ResolveLocationDisplay(status),
                    Mode = status.Mode, Interface = status.Interface, Error = null,
                }, "connect");
            }
            catch (OperationCanceledException)
            {
                // Отмена — не ошибка: состояние осознанно остаётся Connecting, следующий тик
                // PollOnceAsync перепишет его реальным GetStatus; в Error не уходим намеренно.
                throw;
            }
            catch (Exception ex)
            {
                _log.Write($"connect не удался: {OneLine(ex.Message)}");
                SetState(s => s with { Connection = ConnectionState.Error, Error = AppError.From(ex) }, "connect");
            }
        }
        finally
        {
            _operationInFlight = false;
            _opGate.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        // Семафор сериализует connect/disconnect: см. комментарий в ConnectAsync.
        await _opGate.WaitAsync(ct);
        try
        {
            _operationInFlight = true;
            SetState(s => s with { Connection = ConnectionState.Disconnecting, Error = null }, "disconnect");
            try
            {
                await vpn.DisconnectAsync(ct);
                traffic.Reset();
                var session = history.Active;
                history.OnDisconnected(DateTimeOffset.UtcNow);
                LogHistory(session, history.Active);
                SetState(s => s with { Connection = ConnectionState.Disconnected, Location = null, LocationDisplay = null, Interface = null, Traffic = null }, "disconnect");
            }
            catch (OperationCanceledException)
            {
                // Отмена — не ошибка: состояние осознанно остаётся Disconnecting, следующий тик
                // PollOnceAsync перепишет его реальным GetStatus; в Error не уходим намеренно.
                throw;
            }
            catch (Exception ex)
            {
                _log.Write($"disconnect не удался: {OneLine(ex.Message)}");
                SetState(s => s with { Connection = ConnectionState.Error, Error = AppError.From(ex) }, "disconnect");
            }
        }
        finally
        {
            _operationInFlight = false;
            _opGate.Release();
        }
    }

    private void TrackHistory(VpnStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        var before = history.Active;
        if (status.IsConnected && !string.IsNullOrWhiteSpace(status.Location))
        {
            var (city, country, ping) = ResolveLocation(status.Location);
            history.OnConnected(city, country, ping, now);
        }
        else
        {
            history.OnDisconnected(now);
        }
        LogHistory(before, history.Active);
    }

    // Меняем снапшот и пишем в лог только фактическую смену состояния — с источником,
    // чтобы по логу было видно, кто её вызвал.
    private void SetState(Func<AppSnapshot, AppSnapshot> mutate, string source)
    {
        var before = store.Current.Connection;
        store.Set(mutate);
        var after = store.Current.Connection;
        if (after != before) _log.Write($"state: {before} → {after} ({source})");
    }

    // Трекер сам решает, продолжается сессия или начинается новая, поэтому смотрим на активную
    // запись до и после: иначе строка появлялась бы на каждом опросе.
    private void LogHistory(ConnectionHistoryEntry? before, ConnectionHistoryEntry? after)
    {
        if (SameSession(before, after)) return;
        if (before is not null) _log.Write($"история: закрыта сессия {before.City}");
        if (after is not null) _log.Write($"история: открыта сессия {after.City}");
    }

    // Город в записи могут дозаполнить из списка локаций («AMSTERDAM» → «Amsterdam»), поэтому
    // сравниваем без учёта регистра.
    private static bool SameSession(ConnectionHistoryEntry? a, ConnectionHistoryEntry? b) =>
        a is null ? b is null
        : b is not null
            && a.StartedAt == b.StartedAt
            && string.Equals(a.City, b.City, StringComparison.OrdinalIgnoreCase);

    // Текст ошибки приходит из stderr CLI и бывает многострочным: в логе одна запись — одна строка.
    private static string OneLine(string text) =>
        string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    // Человекочитаемая локация «Город, Страна» из известного списка (или просто город).
    private string? ResolveLocationDisplay(VpnStatus status)
    {
        // В фазе Reconnecting туннеля нет, но локация известна — подпись сохраняем.
        if ((!status.IsConnected && status.Phase != VpnPhase.Reconnecting)
            || string.IsNullOrWhiteSpace(status.Location)) return null;
        var (city, country, _) = ResolveLocation(status.Location);
        return string.IsNullOrEmpty(country) ? city : $"{city}, {country}";
    }

    // adguardvpn-cli status отдаёт только город (в верхнем регистре). Country/Ping берём
    // из известного списка локаций по совпадению города без учёта регистра; иначе — fallback.
    private (string City, string Country, int Ping) ResolveLocation(string city)
    {
        foreach (var l in _knownLocations)
            if (string.Equals(l.City, city, StringComparison.OrdinalIgnoreCase))
                return (l.City, l.Country, l.PingMs);
        return (city, "", 0);
    }
}
