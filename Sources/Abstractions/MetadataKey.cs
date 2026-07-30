using System;

namespace Lithify.Abstractions;

/// <summary>
/// 正規化されたメタデータのキー。
/// </summary>
/// <remarks>
/// <para>
/// 正規化により、YAML フロント マターの <c>page_title</c> と AsciiDoc の <c>:page-title:</c> が
/// 同一のキーとして扱われる。形式ごとの記法の揺れを消すのが目的で、
/// これにより形式を意識しない消費者（テーマやテンプレート）が一貫したキーでメタデータを読める。
/// </para>
/// <para>
/// 正規化の内容は「前後の空白の除去」「小文字化」「<c>_</c> を <c>-</c> に変換」の 3 つ。
/// 純粋関数なのでファイル システムを触らずに検証できる。
/// </para>
/// </remarks>
public readonly record struct MetadataKey :
    IComparable<MetadataKey>
{
    private readonly string? _value;

    private MetadataKey(
        string normalized)
    {
        this._value = normalized;
    }

    /// <summary>
    /// 正規化されたキーを取得する。
    /// </summary>
    /// <remarks>
    /// <see langword="default"/> の <see cref="MetadataKey"/> では空文字列になる。
    /// </remarks>
    public string Value =>
        this._value ?? string.Empty;

    /// <summary>
    /// このキーが値を持たない（<see langword="default"/> である）かどうかを示す値を取得する。
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrEmpty(this._value);

    /// <summary>
    /// 生のキー名を正規化して <see cref="MetadataKey"/> を生成する。
    /// </summary>
    /// <param name="raw">正規化前のキー名。</param>
    /// <returns>正規化されたキー。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="raw"/> が <see langword="null"/> である。</exception>
    /// <exception cref="ArgumentException"><paramref name="raw"/> が空または空白のみである。</exception>
    public static MetadataKey Create(
        string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var trimmed = raw.AsSpan().Trim();

        if (trimmed.IsEmpty)
        {
            throw new ArgumentException(Messages.MetadataKeyMustNotBeEmpty, nameof(raw));
        }

        // メタデータ キーは小文字が正規形。YAML と AsciiDoc の慣習がいずれも小文字であり、
        // 大文字への正規化 (CA1308 の推奨) では用を成さない。
#pragma warning disable CA1308 // Normalize strings to uppercase
        return new MetadataKey(trimmed.ToString().ToLowerInvariant().Replace('_', '-'));
#pragma warning restore CA1308
    }

    /// <inheritdoc />
    public int CompareTo(
        MetadataKey other)
    {
        return string.CompareOrdinal(this.Value, other.Value);
    }

    /// <inheritdoc />
    public bool Equals(
        MetadataKey other)
    {
        return string.Equals(this.Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(this.Value);
    }

    /// <summary>
    /// 正規化されたキーを返す。
    /// </summary>
    /// <returns>正規化されたキー。</returns>
    public override string ToString()
    {
        return this.Value;
    }

    /// <summary>
    /// 一方のキーが他方より辞書順で前に並ぶかどうかを判定する。
    /// </summary>
    /// <param name="left">左辺。</param>
    /// <param name="right">右辺。</param>
    /// <returns><paramref name="left"/> が <paramref name="right"/> より前に並ぶ場合は <see langword="true"/>。</returns>
    public static bool operator <(
        MetadataKey left,
        MetadataKey right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>
    /// 一方のキーが他方より辞書順で後に並ぶかどうかを判定する。
    /// </summary>
    /// <param name="left">左辺。</param>
    /// <param name="right">右辺。</param>
    /// <returns><paramref name="left"/> が <paramref name="right"/> より後に並ぶ場合は <see langword="true"/>。</returns>
    public static bool operator >(
        MetadataKey left,
        MetadataKey right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>
    /// 一方のキーが他方より辞書順で前に並ぶか等しいかを判定する。
    /// </summary>
    /// <param name="left">左辺。</param>
    /// <param name="right">右辺。</param>
    /// <returns><paramref name="left"/> が <paramref name="right"/> 以下の場合は <see langword="true"/>。</returns>
    public static bool operator <=(
        MetadataKey left,
        MetadataKey right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// 一方のキーが他方より辞書順で後に並ぶか等しいかを判定する。
    /// </summary>
    /// <param name="left">左辺。</param>
    /// <param name="right">右辺。</param>
    /// <returns><paramref name="left"/> が <paramref name="right"/> 以上の場合は <see langword="true"/>。</returns>
    public static bool operator >=(
        MetadataKey left,
        MetadataKey right)
    {
        return left.CompareTo(right) >= 0;
    }
}
