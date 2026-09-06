using Vantah.Core.Notifications;
using Xunit;

namespace Vantah.Core.Tests.Notifications;

/// <summary>
/// Решение «когда уведомлять». Уведомляем ровно два случая — связь пропала сама и вернулась
/// после такого обрыва; действия пользователя молчат. Время подаём вручную: тест не должен
/// зависеть от настоящих часов.
/// </summary>
public class ConnectionAlertDeciderTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Период опроса статуса в приложении — им и кормим решение.</summary>
    private static readonly TimeSpan Poll = TimeSpan.FromSeconds(4);

    /// <summary>Лента опросов: событие на каждый тик, обратно — только непустые плашки.</summary>
    private sealed class Timeline
    {
        private readonly ConnectionAlertDecider _decider = new();
        private DateTimeOffset _now = Start;

        public List<ConnectionAlert> Alerts { get; } = [];

        public ConnectionAlert Poll(LinkEvent link, int times = 1)
        {
            var last = ConnectionAlert.None;
            for (var i = 0; i < times; i++)
            {
                last = _decider.Observe(link, _now);
                if (last != ConnectionAlert.None) Alerts.Add(last);
                _now += ConnectionAlertDeciderTests.Poll;
            }
            return last;
        }

        /// <summary>Событие без хода времени — например, команда пользователя между опросами.</summary>
        public ConnectionAlert Now(LinkEvent link)
        {
            var alert = _decider.Observe(link, _now);
            if (alert != ConnectionAlert.None) Alerts.Add(alert);
            return alert;
        }
    }

    [Fact]
    public void Retry_after_a_live_tunnel_reports_a_loss()
    {
        var t = new Timeline();
        t.Poll(LinkEvent.Up, times: 3);

        // Первый ответ «Reconnecting» ещё не повод: решение ждёт, пока состояние устоится.
        Assert.Equal(ConnectionAlert.None, t.Poll(LinkEvent.Retrying));
        t.Poll(LinkEvent.Retrying);
        Assert.Equal(ConnectionAlert.Lost, t.Poll(LinkEvent.Retrying));
        Assert.Equal([ConnectionAlert.Lost], t.Alerts);
    }

    [Fact]
    public void Cli_that_drops_to_disconnected_by_itself_reports_a_loss()
    {
        var t = new Timeline();
        t.Poll(LinkEvent.Up, times: 3);

        t.Poll(LinkEvent.Down, times: 3);

        Assert.Equal([ConnectionAlert.Lost], t.Alerts);
    }

    [Fact]
    public void Repeated_retries_report_the_loss_once()
    {
        var t = new Timeline();
        t.Poll(LinkEvent.Up, times: 3);

        // Ретраи kill switch идут каждым опросом — плашка всё равно одна.
        t.Poll(LinkEvent.Retrying, times: 30);

        Assert.Equal([ConnectionAlert.Lost], t.Alerts);
    }

    [Fact]
    public void Recovery_after_a_reported_loss_is_announced()
    {
        var t = new Timeline();
        t.Poll(LinkEvent.Up, times: 3);
        t.Poll(LinkEvent.Retrying, times: 10);

        t.Poll(LinkEvent.Up, times: 10);

        Assert.Equal([ConnectionAlert.Lost, ConnectionAlert.Restored], t.Alerts);
    }

    [Fact]
    public void Recovery_without_a_previous_loss_says_nothing()
    {
        var t = new Timeline();

        // Приложение подняли при выключенном VPN, потом его включили не нашей кнопкой.
        t.Poll(LinkEvent.Down, times: 5);
        t.Poll(LinkEvent.Up, times: 5);

        Assert.Empty(t.Alerts);
    }

    [Fact]
    public void User_disconnect_says_nothing_and_the_following_polls_stay_silent()
    {
        var t = new Timeline();
        t.Poll(LinkEvent.Up, times: 3);

        Assert.Equal(ConnectionAlert.None, t.Now(LinkEvent.UserDisconnect));
        t.Poll(LinkEvent.Down, times: 10);

        Assert.Empty(t.Alerts);
    }

    /// <summary>Ретрай мог долететь уже после команды «Отключить» — плашки всё равно нет.</summary>
    [Fact]
    public void Retry_seen_right_after_user_disconnect_says_nothing()
    {
        var t = new Timeline();
        t.Poll(LinkEvent.Up, times: 3);
        t.Now(LinkEvent.UserDisconnect);

        t.Poll(LinkEvent.Retrying, times: 10);

        Assert.Empty(t.Alerts);
    }

    /// <summary>После «Подключить» инцидент закрыт: поднявшийся туннель — не «восстановление».</summary>
    [Fact]
    public void User_connect_closes_the_incident()
    {
        var t = new Timeline();
        t.Poll(LinkEvent.Up, times: 3);
        t.Poll(LinkEvent.Down, times: 5);
        Assert.Equal([ConnectionAlert.Lost], t.Alerts);

        t.Now(LinkEvent.UserConnect);
        t.Poll(LinkEvent.Up, times: 10);

        Assert.Equal([ConnectionAlert.Lost], t.Alerts);
    }

    /// <summary>
    /// Метания связи не должны сыпать плашками: пока состояние скачет от опроса к опросу,
    /// говорить не о чем — ни одного уведомления.
    /// </summary>
    [Fact]
    public void Flapping_link_produces_no_alerts_at_all()
    {
        var t = new Timeline();
        t.Poll(LinkEvent.Up, times: 3);

        for (var i = 0; i < 20; i++)
        {
            t.Poll(LinkEvent.Retrying);
            t.Poll(LinkEvent.Up);
        }

        Assert.Empty(t.Alerts);
    }

    /// <summary>
    /// Метания, закончившиеся настоящим обрывом: сначала тишина, потом ровно одна плашка,
    /// когда связь устоялась в «лежит».
    /// </summary>
    [Fact]
    public void Flapping_that_settles_into_a_real_loss_reports_it_once()
    {
        var t = new Timeline();
        t.Poll(LinkEvent.Up, times: 3);

        for (var i = 0; i < 5; i++)
        {
            t.Poll(LinkEvent.Retrying);
            t.Poll(LinkEvent.Up);
        }
        Assert.Empty(t.Alerts);

        t.Poll(LinkEvent.Retrying, times: 10);

        Assert.Equal([ConnectionAlert.Lost], t.Alerts);
    }

    /// <summary>Обрыв длиной в один опрос не порождает ни одной плашки.</summary>
    [Fact]
    public void A_blip_shorter_than_the_settle_window_says_nothing()
    {
        var t = new Timeline();
        t.Poll(LinkEvent.Up, times: 3);

        t.Poll(LinkEvent.Retrying);
        t.Poll(LinkEvent.Up, times: 10);

        Assert.Empty(t.Alerts);
    }

    /// <summary>Второй обрыв после восстановления — снова плашка: это уже другое событие.</summary>
    [Fact]
    public void A_second_loss_after_a_recovery_is_reported_again()
    {
        var t = new Timeline();
        t.Poll(LinkEvent.Up, times: 3);
        t.Poll(LinkEvent.Retrying, times: 10);
        t.Poll(LinkEvent.Up, times: 10);

        t.Poll(LinkEvent.Retrying, times: 10);

        Assert.Equal(
            [ConnectionAlert.Lost, ConnectionAlert.Restored, ConnectionAlert.Lost],
            t.Alerts);
    }
}
