using System;

using JetBrains.Annotations;

namespace Lithify.Abstractions;

/// <summary>
/// サイト ルートからの相対パスで表されるコンテンツの位置。
/// </summary>
/// <remarks>
/// <para>
/// 常に <c>/</c> 区切りに正規化され、絶対パスとルートより上に遡るパスは拒否される。
/// これによりパスを扱うコードがプラットフォーム差を意識せずに済み、
/// またコンテンツ パスがサイト ルートの外を指せないことが型として保証される。
/// </para>
/// <para>
/// 大文字小文字は区別する。ファイル システムが区別しない環境でも出力される URL は区別するため、
/// 区別しない扱いにすると環境によって出力が変わってしまう。
/// </para>
/// </remarks>
public readonly record struct ContentPath :
    IComparable<ContentPath>
{
    private readonly string? _value;

    /// <summary>
    /// 指定した相対パスから <see cref="ContentPath"/> を生成する。
    /// </summary>
    /// <param name="value">サイト ルートからの相対パス。<c>/</c> と <c>\</c> のどちらの区切りでもよい。</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> が <see langword="null"/> である。</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> が空である、絶対パスである、またはサイト ルートより上に遡っている。
    /// </exception>
    public ContentPath(
        string value)
    {
        this._value = PathNormalizer.Normalize(value, PathKind.Content, nameof(value));
    }

    /// <summary>
    /// 正規化された相対パスを取得する。
    /// </summary>
    /// <remarks>
    /// <see langword="default"/> の <see cref="ContentPath"/> では空文字列になる。
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
    /// 拡張子を除いたファイル名を取得する。
    /// </summary>
    public ReadOnlySpan<char> FileNameWithoutExtension =>
        PathNormalizer.GetFileNameWithoutExtension(this.Value);

    /// <summary>
    /// 親ディレクトリのパスを取得する。親がない場合は <see langword="default"/>。
    /// </summary>
    public ContentPath Directory =>
        PathNormalizer.GetDirectory(this.Value) is { } directory
            ? new ContentPath(directory)
            : default;

    /// <summary>
    /// このパスの下に相対パスを連結する。
    /// </summary>
    /// <param name="relative">
    /// 連結する相対パス。<c>..</c> を含んでもよいが、結果がサイト ルートの外に出てはならない。
    /// </param>
    /// <returns>連結されたパス。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="relative"/> が <see langword="null"/> である。</exception>
    /// <exception cref="ArgumentException">連結結果が空になる、またはサイト ルートの外に出る。</exception>
    /// <remarks>
    /// include や相対リンクの解決に用いる。<c>..</c> の解決は正規化時に行われるため、
    /// サイト ルートを脱出する参照はここで例外になる。
    /// </remarks>
    [Pure]
    public ContentPath Combine(
        string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        return this.IsEmpty
            ? new ContentPath(relative)
            : new ContentPath(string.Concat(this.Value, "/", relative));
    }

    /// <summary>
    /// 拡張子を差し替えたパスを返す。
    /// </summary>
    /// <param name="extension">新しい拡張子。先頭の <c>.</c> は省略できる。空文字列なら拡張子を取り除く。</param>
    /// <returns>拡張子が差し替えられたパス。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="extension"/> が <see langword="null"/> である。</exception>
    [Pure]
    public ContentPath WithExtension(
        string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        return new ContentPath(PathNormalizer.ReplaceExtension(this.Value, extension));
    }

    /// <inheritdoc />
    public int CompareTo(
        ContentPath other)
    {
        return string.CompareOrdinal(this.Value, other.Value);
    }

    /// <inheritdoc />
    public bool Equals(
        ContentPath other)
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
        ContentPath left,
        ContentPath right)
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
        ContentPath left,
        ContentPath right)
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
        ContentPath left,
        ContentPath right)
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
        ContentPath left,
        ContentPath right)
    {
        return left.CompareTo(right) >= 0;
    }
}
