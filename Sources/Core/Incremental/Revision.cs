using System;
using System.Globalization;

using JetBrains.Annotations;

namespace Lithify.Core.Incremental;

/// <summary>
/// 計算グラフの世代番号。入力が変化するたびに単調増加する。
/// </summary>
/// <param name="Value">世代番号。</param>
/// <remarks>
/// <para>
/// リビジョンは 2 つの役割を持つ。1 つは「このノードは今の世代で既に検証済みか」の判定であり、
/// 1 回のビルド中に同じノードを何度も再検証しないためのもの。
/// もう 1 つは <see cref="Computed{T}.ChangedAt"/> による early cutoff であり、
/// 再計算しても値が変わらなかったノードの世代を据え置くことで下流の再計算を止める。
/// </para>
/// <para>
/// バックグラウンド ビルドはこの番号を境界として中断する。
/// 進行中の評価の結果を、既に古くなったリビジョンのものとしてキャッシュしてはならない。
/// </para>
/// </remarks>
public readonly record struct Revision(long Value) :
    IComparable<Revision>
{
    /// <summary>
    /// 最初のリビジョン。
    /// </summary>
    /// <remarks>
    /// <see langword="default"/>（<see cref="None"/>）と区別できるよう 1 から始める。
    /// これにより「まだ一度も計算されていない」状態を <see langword="default"/> で表せる。
    /// </remarks>
    public static Revision Initial { get; } = new(1);

    /// <summary>
    /// リビジョンが存在しないことを表す値。
    /// </summary>
    public static Revision None => default;

    /// <summary>
    /// このリビジョンが <see cref="None"/> であるかどうかを示す値を取得する。
    /// </summary>
    public bool IsNone =>
        this.Value <= 0;

    /// <summary>
    /// 次のリビジョンを返す。
    /// </summary>
    /// <returns>次のリビジョン。</returns>
    /// <exception cref="OverflowException">リビジョンが <see cref="long.MaxValue"/> を超えた。</exception>
    [Pure]
    public Revision Next()
    {
        return new Revision(this.Value + 1);
    }

    /// <inheritdoc />
    public int CompareTo(
        Revision other)
    {
        return this.Value.CompareTo(other.Value);
    }

    /// <summary>
    /// 世代番号を返す。
    /// </summary>
    /// <returns>世代番号の文字列表現。</returns>
    public override string ToString()
    {
        return this.Value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 一方のリビジョンが他方より古いかどうかを判定する。
    /// </summary>
    /// <param name="left">左辺。</param>
    /// <param name="right">右辺。</param>
    /// <returns><paramref name="left"/> が <paramref name="right"/> より古い場合は <see langword="true"/>。</returns>
    public static bool operator <(
        Revision left,
        Revision right)
    {
        return left.Value < right.Value;
    }

    /// <summary>
    /// 一方のリビジョンが他方より新しいかどうかを判定する。
    /// </summary>
    /// <param name="left">左辺。</param>
    /// <param name="right">右辺。</param>
    /// <returns><paramref name="left"/> が <paramref name="right"/> より新しい場合は <see langword="true"/>。</returns>
    public static bool operator >(
        Revision left,
        Revision right)
    {
        return left.Value > right.Value;
    }

    /// <summary>
    /// 一方のリビジョンが他方より古いか等しいかを判定する。
    /// </summary>
    /// <param name="left">左辺。</param>
    /// <param name="right">右辺。</param>
    /// <returns><paramref name="left"/> が <paramref name="right"/> 以下の場合は <see langword="true"/>。</returns>
    public static bool operator <=(
        Revision left,
        Revision right)
    {
        return left.Value <= right.Value;
    }

    /// <summary>
    /// 一方のリビジョンが他方より新しいか等しいかを判定する。
    /// </summary>
    /// <param name="left">左辺。</param>
    /// <param name="right">右辺。</param>
    /// <returns><paramref name="left"/> が <paramref name="right"/> 以上の場合は <see langword="true"/>。</returns>
    public static bool operator >=(
        Revision left,
        Revision right)
    {
        return left.Value >= right.Value;
    }
}
