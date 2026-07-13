using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Platform;
using Vantah.Core.Models;

namespace Vantah.App.Tray;

/// <summary>
/// Иконки трея для одного комплекта. Грузятся один раз: Apply() дёргается на каждое
/// изменение состояния, читать файл с диска каждый раз незачем.
/// </summary>
public sealed class TrayIconSet
{
    private readonly Dictionary<string, WindowIcon> _byGlyph = new();

    public TrayIconSet(TrayIconPolarity polarity)
    {
        foreach (var state in Enum.GetValues<ConnectionState>())
        {
            var glyph = TrayIconResolver.GlyphName(state);
            if (_byGlyph.ContainsKey(glyph)) continue;

            // Исключение загрузки намеренно НЕ глушится: битый ресурс должен падать на старте
            // и в тесте, а не вырождаться в пустой трей.
            using var stream = AssetLoader.Open(new Uri(TrayIconResolver.AssetUri(state, polarity)));
            _byGlyph[glyph] = new WindowIcon(stream);
        }
    }

    /// <summary>Иконка для состояния. Null не возвращается: все глифы загружены в конструкторе.</summary>
    public WindowIcon For(ConnectionState state) => _byGlyph[TrayIconResolver.GlyphName(state)];
}
