namespace Vantah.Core.Models;

// Unknown — стартовое состояние до первого опроса CLI. Добавлено В КОНЕЦ, чтобы не сдвинуть
// числовые значения уже существующих членов.
public enum ConnectionState { Disconnected, Connecting, Connected, Disconnecting, Error, Unknown }
