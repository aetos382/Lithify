using System;

using JetBrains.Annotations;

namespace Lithify.Abstractions;

/// <summary>
/// 出力ルートからの相対パスで表される生成物の位置。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ContentPath"/> と同じ正規化規則に従うが、意味が異なるので別の型にしている。
/// 両者を混同すると「入力パスを出力先として渡す」種類の誤りが型検査を通ってしまう。
/// </para>
/// <para>
/// 出力ストアはルート付きなので、この型が絶対パスを表すことはない。
/// </para>
/// </remarks>
public readonly record struct OutputPath :
    IComparable<OutputPath>
{
    private readonly string? _value;

    /// <summary>
    /// 指定した相対パスから <see cref="OutputPath"/> を生成する。
    /// </summary>
    /// <param name="value">出力ルートからの相対パス。<c>/</c> と <c>\</c> のどちらの区切りでもよい。</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> が <see langword="null"/> である。</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> が空である、絶対パスである、または出力ルートより上に遡っている。
    /// </exception>
    public OutputPath(
        string value)
    {
        this._value = PathNormalizer.Normalize(value, PathKind.Output, nameof(value));
    }

    /// <summary>
    /// 正規化された相対パスを取得する。
    /// </summary>
    /// <remarks>
    /// <see langword="default"/> の <see cref="OutputPath"/> では空文字列になる。
    /// </remarks>
    public string Value =>
        this._value ?? string.Empty;

    /// <summary>
    /// このパスが値を持たない（<see langword="default"/> である）かどうかを示す値を取得する。
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrEmpty(this._value);

    /// <summary>
    /// 拡張子を取得する（先頭の <c>.</c> を含む）。拡張子がない場合は空。
    /// </summary>
    public ReadOnlySpan<char> Extension =>
        PathNormalizer.GetExtension(this.Value);

    /// <summary>
    /// ファイル名を取得する。
    /// </summary>
    public ReadOnlySpan<char> FileName =>
        PathNormalizer.GetFileName(this.Value);

    /// <summary>
    /// 親ディレクトリのパスを取得する。親がない場合は <see langword="default"/>。
    /// </summary>
    public OutputPath Directory =>
        PathNormalizer.GetDirectory(this.Value) is { } directory
            ? new OutputPath(directory)
            : default;

    /// <summary>
    /// このパスの下に相対パスを連結する。
    /// </summary>
    /// <param name="relative">連結する相対パス。</param>
    /// <returns>連結されたパス。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="relative"/> が <see langword="null"/> である。</exception>
    /// <exception cref="ArgumentException">連結結果が空になる、または出力ルートの外に出る。</exception>
    [Pure]
    public OutputPath Combine(
        string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        return this.IsEmpty
            ? new OutputPath(relative)
            : new OutputPath(string.Concat(this.Value, "/", relative));
    }

    /// <summary>
    /// このパスに対応する URL のパス部分を返す。
    /// </summary>
    /// <returns>先頭に <c>/</c> が付いた絶対 URL パス。</returns>
    /// <remarks>
    /// <c>index.html</c> はディレクトリを指す URL に畳む（<c>posts/hello/index.html</c> → <c>/posts/hello/</c>）。
    /// permalink が末尾スラッシュ形式のときに、出力パスと URL を 1 対 1 に対応させるための規則。
    /// </remarks>
    // サイト ルートからの相対 URL パスであって完全な URI ではないため、
    // CA1055 が推奨する Uri では表現できない。
#pragma warning disable CA1055 // URI-like return values should not be strings
    [Pure]
    public string ToUrlPath()
#pragma warning restore CA1055
    {
        const string IndexFileName = "index.html";

        if (this.IsEmpty)
        {
            return "/";
        }

        var value = this.Value;

        if (!PathNormalizer.GetFileName(value).SequenceEqual(IndexFileName))
        {
            return string.Concat("/", value);
        }

        return value.Length == IndexFileName.Length
            ? "/"
            : string.Concat("/", value.AsSpan()[..^IndexFileName.Length]);
    }

    /// <inheritdoc />
    public int CompareTo(
        OutputPath other)
    {
        return string.CompareOrdinal(this.Value, other.Value);
    }

    /// <inheritdoc />
    public bool Equals(
        OutputPath other)
    {
        return string.Equals(this.Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(this.Value);
    }

    /// <summary>
    /// 正規化された相対パスを返す。
    /// </summary>
    /// <returns>正規化された相対パス。</returns>
    public override string ToString()
    {
        return this.Value;
    }

    /// <summary>
    /// 一方のパスが他方より辞書順で前に並ぶかどうかを判定する。
    /// </summary>
    /// <param name="left">左辺。</param>
    /// <param name="right">右辺。</param>
    /// <returns><paramref name="left"/> が <paramref name="right"/> より前に並ぶ場合は <see langword="true"/>。</returns>
    public static bool operator <(
        OutputPath left,
        OutputPath right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>
    /// 一方のパスが他方より辞書順で後に並ぶかどうかを判定する。
    /// </summary>
    /// <param name="left">左辺。</param>
    /// <param name="right">右辺。</param>
    /// <returns><paramref name="left"/> が <paramref name="right"/> より後に並ぶ場合は <see langword="true"/>。</returns>
    public static bool operator >(
        OutputPath left,
        OutputPath right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>
    /// 一方のパスが他方より辞書順で前に並ぶか等しいかを判定する。
    /// </summary>
    /// <param name="left">左辺。</param>
    /// <param name="right">右辺。</param>
    /// <returns><paramref name="left"/> が <paramref name="right"/> 以下の場合は <see langword="true"/>。</returns>
    public static bool operator <=(
        OutputPath left,
        OutputPath right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// 一方のパスが他方より辞書順で後に並ぶか等しいかを判定する。
    /// </summary>
    /// <param name="left">左辺。</param>
    /// <param name="right">右辺。</param>
    /// <returns><paramref name="left"/> が <paramref name="right"/> 以上の場合は <see langword="true"/>。</returns>
    public static bool operator >=(
        OutputPath left,
        OutputPath right)
    {
        return left.CompareTo(right) >= 0;
    }
}
