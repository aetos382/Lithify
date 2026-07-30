using System;

namespace Lithify.Abstractions;

/// <summary>
/// シンタックス ハイライトのトークンに割り当てられるスコープ名。
/// </summary>
/// <remarks>
/// TextMate のスコープ命名規約（<c>keyword.control</c>、<c>string.quoted.double</c> など）に従う。
/// <see langword="enum"/> にしないのは、スコープの語彙が文法ファイルごとに定義され、
/// あらかじめ列挙できないためである。
/// </remarks>
public readonly record struct ScopeName
{
    private readonly string? _value;

    /// <summary>
    /// 指定した名前から <see cref="ScopeName"/> を生成する。
    /// </summary>
    /// <param name="value">スコープ名。</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> が <see langword="null"/> である。</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> が空または空白のみである。</exception>
    public ScopeName(
        string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(Messages.ScopeNameMustNotBeEmpty, nameof(value));
        }

        this._value = value.Trim();
    }

    /// <summary>
    /// スコープ名を取得する。
    /// </summary>
    /// <remarks>
    /// <see langword="default"/> の <see cref="ScopeName"/> では空文字列になる。
    /// </remarks>
    public string Value =>
        this._value ?? string.Empty;

    /// <summary>
    /// このスコープ名が値を持たない（<see langword="default"/> である）かどうかを示す値を取得する。
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrEmpty(this._value);

    /// <inheritdoc />
    public bool Equals(
        ScopeName other)
    {
        return string.Equals(this.Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(this.Value);
    }

    /// <summary>
    /// スコープ名を返す。
    /// </summary>
    /// <returns>スコープ名。</returns>
    public override string ToString()
    {
        return this.Value;
    }
}
