using Vantah.Core.Models;

namespace Vantah.App.Tray;

/// <summary>
/// Чистая логика иконки трея: какой глиф показывать в каком состоянии.
/// Состояние несёт форма знака (нет узлов / контурные узлы / залитые узлы) — на 16px и при
/// нарушении цветовосприятия она единственная опора. Цвет состояния (серый / янтарный /
/// зелёный) лишь дублирует сигнал и запечён в самих ICO: Avalonia кладёт в трей растр,
/// а не symbolic-иконку, перекрасить его на лету всё равно нечем.
/// </summary>
public static class TrayIconResolver
{
    public static string GlyphName(ConnectionState state) => state switch
    {
        ConnectionState.Connected => "connected",
        ConnectionState.Connecting or ConnectionState.Disconnecting => "connecting",
        // Disconnected и Error: защиты нет. Отдельный аварийный глиф на 16px не читается,
        // а причина ошибки уходит в тултип.
        _ => "disconnected",
    };

    public static string AssetUri(ConnectionState state) =>
        $"avares://Vantah.App/Assets/tray/{GlyphName(state)}.ico";
}
