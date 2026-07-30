using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Lithify.Abstractions;

namespace Lithify.Core.Incremental;

/// <summary>
/// 計算ノードを保持し、リビジョンの進行と再検証を管理するグラフ。
/// </summary>
/// <remarks>
/// <para>
/// グラフはノードの所有者である。同じ <see cref="NodeId"/> に対して常に同じノード インスタンスを返すため、
/// メモ化とノード単位の single-flight が成立する。
/// </para>
/// <para>
/// 再検証アルゴリズムは次のとおり。詳細は <c>docs/architecture.md</c> に記述する。
/// </para>
/// <list type="number">
/// <item><description>ノードの検証済みリビジョンが現在のリビジョンと等しければ、キャッシュした値を返す。</description></item>
/// <item><description>
/// 依存を再帰的に検証する。すべての依存の <see cref="Computed{T}.ChangedAt"/> が
/// ノードの検証済みリビジョン以下であれば、再計算せず検証済みリビジョンだけを進める（early cutoff）。
/// </description></item>
/// <item><description>
/// いずれかの依存が変化していれば再計算する。新しいフィンガープリントが前回と一致すれば
/// <see cref="Computed{T}.ChangedAt"/> を据え置き、下流の再計算を止める。
/// </description></item>
/// </list>
/// </remarks>
public interface IComputeGraph
{
    /// <summary>
    /// 現在のリビジョンを取得する。
    /// </summary>
    Revision CurrentRevision { get; }

    /// <summary>
    /// 指定した識別子のノードを取得する。存在しなければ <paramref name="factory"/> で生成して登録する。
    /// </summary>
    /// <typeparam name="T">値の型。</typeparam>
    /// <param name="id">ノードの識別子。</param>
    /// <param name="factory">ノードの生成処理。同じ識別子に対して 1 回だけ呼ばれる。</param>
    /// <returns>登録済みのノード。</returns>
    /// <remarks>
    /// 既に別の型で登録されている識別子を指定した場合は例外になる。
    /// 識別子は種別を含むため、正しく命名していれば衝突しない。
    /// </remarks>
    IComputeNode<T> GetOrAdd<T>(
        NodeId id,
        Func<NodeId, IComputeNode<T>> factory);

    /// <summary>
    /// 入力の変化を反映してリビジョンを進める。
    /// </summary>
    /// <param name="changedPaths">変化した入力のパス。</param>
    /// <returns>新しいリビジョン。</returns>
    /// <remarks>
    /// <para>
    /// 変化していない場合でもリビジョンは進む。進めない最適化は、変化の有無の判定を
    /// リビジョンの管理に混ぜることになり、ここより下の層で行うべきである。
    /// </para>
    /// <para>
    /// 進行中のバックグラウンド評価は、このリビジョンをもって中断される。
    /// </para>
    /// </remarks>
    Revision Invalidate(
        IEnumerable<ContentPath> changedPaths);

    /// <summary>
    /// 指定したノードの値を要求する。グラフのルートからの入り口。
    /// </summary>
    /// <typeparam name="T">値の型。</typeparam>
    /// <param name="node">要求するノード。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>評価結果。</returns>
    ValueTask<Computed<T>> DemandAsync<T>(
        IComputeNode<T> node,
        CancellationToken cancellationToken = default);
}
