namespace Vantah.Core.Models;

// Unknown — стартовое состояние до первого опроса CLI. CliMissing — исполняемого файла CLI нет.
// Новые члены добавляются В КОНЕЦ, чтобы не сдвинуть числовые значения уже существующих.
public enum ConnectionState { Disconnected, Connecting, Connected, Disconnecting, Error, Unknown, CliMissing }
