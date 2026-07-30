using System;

namespace Lithify.Abstractions;

/// <summary>
/// ページを構成するフラグメントの識別子。
/// </summary>
/// <remarks>
/// <para>
/// フラグメントは独立に評価・メモ化される描画単位。<c>body</c> は記事ソースのみに依存し、
/// <c>sidebar-tags</c> はサイト横断のタグ索引のみに依存する。
/// この分離により、新しい記事を追加してもサイドバーだけが再評価され、
/// 本文は再描画されない（R8）。
/// </para>
/// <para>
/// 名前を <see langword="enum"/> にしないのは、フラグメントの種類が
/// テーマやプラグインによって増えるためである。
/// </para>
/// </remarks>
public readonly record struct FragmentId
{
    private readonly string? _value;

    /// <summary>
    /// 指定した名前から <see cref="FragmentId"/> を生成する。
    /// </summary>
    /// <param name="value">フラグメントの名前。</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> が <see langword="null"/> である。</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> が空または空白のみである。</exception>
    public FragmentId(
        string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(Messages.FragmentIdMustNotBeEmpty, nameof(value));
        }

        this._value = value.Trim();
    }

    /// <summary>
    /// 記事本文のフラグメント。
    /// </summary>
    public static FragmentId Body { get; } = new("body");

    /// <summary>
    /// サイト横断のタグ一覧のフラグメント。
    /// </summary>
    public static FragmentId SidebarTags { get; } = new("sidebar-tags");

    /// <summary>
    /// サイト横断の月別アーカイブ一覧のフラグメント。
    /// </summary>
    public static FragmentId SidebarArchive { get; } = new("sidebar-archive");

    /// <summary>
    /// フラグメントの名前を取得する。
    /// </summary>
    /// <remarks>
    /// <see langword="default"/> の <see cref="FragmentId"/> では空文字列になる。
    /// </remarks>
    public string Value =>
        this._value ?? string.Empty;

    /// <summary>
    /// この識別子が値を持たない（<see langword="default"/> である）かどうかを示す値を取得する。
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrEmpty(this._value);

    /// <inheritdoc />
    public bool Equals(
        FragmentId other)
    {
        return string.Equals(this.Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(this.Value);
    }

    /// <summary>
    /// フラグメントの名前を返す。
    /// </summary>
    /// <returns>フラグメントの名前。</returns>
    public override string ToString()
    {
        return this.Value;
    }
}
