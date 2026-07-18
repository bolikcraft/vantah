namespace Vantah.Core.Auth;

/// <summary>Пароль/код в памяти как char[]; занулять сразу после использования. Никогда не логировать.</summary>
public sealed class SecureCredential : IDisposable
{
    private char[]? _chars;

    public SecureCredential(char[] chars) => _chars = chars ?? throw new ArgumentNullException(nameof(chars));

    public char[] Reveal() =>
        _chars ?? throw new ObjectDisposedException(nameof(SecureCredential));

    public bool IsEmpty => _chars is null || _chars.Length == 0;

    public void Clear()
    {
        if (_chars is not null) Array.Clear(_chars, 0, _chars.Length);
        _chars = null;
    }

    public void Dispose() => Clear();

    public override string ToString() => "***";   // никогда не раскрываем
}
