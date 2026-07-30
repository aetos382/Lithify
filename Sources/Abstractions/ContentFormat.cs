using System;

namespace Lithify.Abstractions;

/// <summary>
/// コンテンツの記述形式。
/// </summary>
/// <remarks>
/// <para>
/// <see langword="enum"/> ではなく開いた構造体にしているのは、パーサーが独立パッケージとして
/// 誰でも追加できるという建前と、形式の追加が <c>Lithify.Abstractions</c> の改版を要求することが
/// 矛盾するためである。既知の形式は静的プロパティとして便宜的に定義するだけで、
/// 利用者は任意の名前で新しい形式を作れる。
/// </para>
/// <para>
/// 名前は小文字に正規化され、比較は大文字小文字を区別しない。
/// </para>
/// </remarks>
public readonly record struct ContentFormat
{
    private readonly string? _value;

    /// <summary>
    /// 指定した名前から <see cref="ContentFormat"/> を生成する。
    /// </summary>
    /// <param name="value">形式の名前。</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> が <see langword="null"/> である。</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> が空または空白のみである。</exception>
    public ContentFormat(
        string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(Messages.ContentFormatMustNotBeEmpty, nameof(value));
        }

        // 形式名は小文字が正規形。ファイル拡張子や MIME サブタイプと突き合わせるため、
        // 大文字への正規化 (CA1308 の推奨) では用を成さない。
#pragma warning disable CA1308 // Normalize strings to uppercase
        this._value = value.Trim().ToLowerInvariant();
#pragma warning restore CA1308
    }

    /// <summary>
    /// Markdown 形式。
    /// </summary>
    public static ContentFormat Markdown { get; } = new("markdown");

    /// <summary>
    /// AsciiDoc 形式。
    /// </summary>
    public static ContentFormat AsciiDoc { get; } = new("asciidoc");

    /// <summary>
    /// HTML 形式。パーサーを通さずそのまま出力されるコンテンツを表す。
    /// </summary>
    public static ContentFormat Html { get; } = new("html");

    /// <summary>
    /// 正規化された形式の名前を取得する。
    /// </summary>
    /// <remarks>
    /// <see langword="default"/> の <see cref="ContentFormat"/> では空文字列になる。
    /// </remarks>
    public string Value =>
        this._value ?? string.Empty;

    /// <summary>
    /// この形式が値を持たない（<see langword="default"/> である）かどうかを示す値を取得する。
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrEmpty(this._value);

    /// <inheritdoc />
    public bool Equals(
        ContentFormat other)
    {
        return string.Equals(this.Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(this.Value);
    }

    /// <summary>
    /// 正規化された形式の名前を返す。
    /// </summary>
    /// <returns>形式の名前。</returns>
    public override string ToString()
    {
        return this.Value;
    }
}
