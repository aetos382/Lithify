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
/// 名前は正規化<em>しない</em>。<see cref="ContentFormat"/> を組み立てるのは
/// パーサー パッケージ（<see cref="IContentParser.SupportedFormats"/>）と拡張子の対応表
/// （<see cref="IContentFormatRegistry.TryGetFormat"/>）だけで、いずれもコードである。
/// 利用者が書くコンテンツとの接触面はファイルの<em>拡張子</em>であって形式名ではないので、
/// 表記の揺れは吸収すべき入力の多様性ではなく単にコードの誤りである
/// （この点が、利用者の書いた名前を受け取る <see cref="MetadataKey"/> との違いである）。
/// </para>
/// <para>
/// したがって比較は <see cref="StringComparison.Ordinal"/> で、厳密に一致しない名前は
/// 別の形式として扱われる。誤りは「その形式を扱えるパーサーが無い」という形で露見する。
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
    /// <exception cref="ArgumentException"><paramref name="value"/> が空である。</exception>
    /// <remarks>
    /// 空白のみの名前や前後に空白を含む名前は弾かず、そのまま保持する。
    /// 呼び出し側はコードなので、それらは書いた通りに一致しない名前として扱われる。
    /// </remarks>
    public ContentFormat(
        string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        this._value = value;
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
    /// 形式の名前を取得する。
    /// </summary>
    /// <remarks>
    /// <see langword="default"/> の <see cref="ContentFormat"/> では空文字列になる。
    /// </remarks>
    public string Value => this._value ?? string.Empty;

    /// <summary>
    /// この形式が値を持たない（<see langword="default"/> である）かどうかを示す値を取得する。
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(this._value);

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
    /// 形式の名前を返す。
    /// </summary>
    /// <returns>形式の名前。</returns>
    public override string ToString()
    {
        return this.Value;
    }
}
