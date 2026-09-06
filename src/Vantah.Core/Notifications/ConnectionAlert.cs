namespace Vantah.Core.Notifications;

/// <summary>О чём надо сказать пользователю. Код события: строку собирает UI на своём языке.</summary>
public enum ConnectionAlert
{
    /// <summary>Говорить нечего.</summary>
    None,

    /// <summary>Связь пропала сама, без команды пользователя.</summary>
    Lost,

    /// <summary>Связь вернулась после обрыва, о котором мы уже сообщили.</summary>
    Restored,
}
