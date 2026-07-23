namespace Vantah.Core.Update;

/// <summary>Исход `adguardvpn-cli update`.</summary>
public enum UpdateOutcome { Updated, AlreadyLatest, Failed }

/// <summary>Результат установки обновления CLI: исход + сырой вывод CLI (английский, не переводится).</summary>
public sealed record UpdateResult(UpdateOutcome Outcome, string Output);
