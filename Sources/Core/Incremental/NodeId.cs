using System;

using Lithify.Abstractions;

namespace Lithify.Core.Incremental;

/// <summary>
/// 計算グラフ上のノードの識別子。
/// </summary>
/// <remarks>
/// <para>
/// 種別と引数から成る安定した名前。<c>parse:posts/hello.md</c> や
/// <c>fragment:body:posts/hello.md</c> のように、ビルドを跨いで同じノードが同じ識別子になる。
/// これによりディスク上のビルド キャッシュから前回の結果を引き当てられる。
/// </para>
/// <para>
/// オブジェクトの参照同一性ではなく識別子でノードを引くのは、
/// 前回のビルドで作られたノード インスタンスが今回のビルドには存在しないためである。
/// </para>
/// </remarks>
public readonly record struct NodeId
{
    private const char Separator = ':';

    private readonly string? _value;

    /// <summary>
    /// 指定した文字列から <see cref="NodeId"/> を生成する。
    /// </summary>
    /// <param name="value">識別子。</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> が <see langword="null"/> である。</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> が空または空白のみである。</exception>
    public NodeId(
        string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(Messages.NodeIdMustNotBeEmpty, nameof(value));
        }

        this._value = value;
    }

    /// <summary>
    /// 識別子を取得する。
    /// </summary>
    /// <remarks>
    /// <see langword="default"/> の <see cref="NodeId"/> では空文字列になる。
    /// </remarks>
    public string Value =>
        this._value ?? string.Empty;

    /// <summary>
    /// この識別子が値を持たない（<see langword="default"/> である）かどうかを示す値を取得する。
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrEmpty(this._value);

    /// <summary>
    /// このノードの内容から決まるフィンガープリントを取得する。
    /// </summary>
    /// <remarks>
    /// キャッシュのキーとして固定長で扱いたい場面のために用意している。
    /// </remarks>
    public Fingerprint Fingerprint =>
        Fingerprint.OfString(this.Value);

    /// <summary>
    /// 種別のみからノード識別子を生成する。
    /// </summary>
    /// <param name="kind">ノードの種別（<c>site-index</c> 等）。</param>
    /// <returns>生成された識別子。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="kind"/> が <see langword="null"/> である。</exception>
    /// <exception cref="ArgumentException"><paramref name="kind"/> が空または空白のみである。</exception>
    public static NodeId Create(
        string kind)
    {
        return new NodeId(kind);
    }

    /// <summary>
    /// 種別と 1 つの引数からノード識別子を生成する。
    /// </summary>
    /// <param name="kind">ノードの種別（<c>parse</c> 等）。</param>
    /// <param name="argument">引数（対象のパス等）。</param>
    /// <returns>生成された識別子。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="kind"/> または <paramref name="argument"/> が <see langword="null"/> である。
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="kind"/> が空または空白のみである。</exception>
    public static NodeId Create(
        string kind,
        string argument)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(argument);

        return new NodeId(string.Concat(kind, Separator.ToString(), argument));
    }

    /// <summary>
    /// 種別と 2 つの引数からノード識別子を生成する。
    /// </summary>
    /// <param name="kind">ノードの種別（<c>fragment</c> 等）。</param>
    /// <param name="argument1">第 1 引数（フラグメント名等）。</param>
    /// <param name="argument2">第 2 引数（対象のパス等）。</param>
    /// <returns>生成された識別子。</returns>
    /// <exception cref="ArgumentNullException">いずれかの引数が <see langword="null"/> である。</exception>
    /// <exception cref="ArgumentException"><paramref name="kind"/> が空または空白のみである。</exception>
    public static NodeId Create(
        string kind,
        string argument1,
        string argument2)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(argument1);
        ArgumentNullException.ThrowIfNull(argument2);

        return new NodeId(string.Join(Separator, kind, argument1, argument2));
    }

    /// <inheritdoc />
    public bool Equals(
        NodeId other)
    {
        return string.Equals(this.Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(this.Value);
    }

    /// <summary>
    /// 識別子を返す。
    /// </summary>
    /// <returns>識別子。</returns>
    public override string ToString()
    {
        return this.Value;
    }
}
