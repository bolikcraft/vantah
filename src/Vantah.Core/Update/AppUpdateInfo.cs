namespace Vantah.Core.Update;

/// <summary>Найденный релиз Vantah: тег как есть (например «v0.2.0») и ссылка на страницу релиза.</summary>
public sealed record AppUpdateInfo(string Version, string ReleaseUrl);
