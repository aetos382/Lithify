using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Lithify.Abstractions;

namespace Lithify.Core.Building;

/// <summary>
/// サイトをビルドする。
/// </summary>
/// <remarks>
/// <para>
/// 計算グラフの上に乗る薄い層である。ビルドとは「出力ページの
/// <see cref="Lithify.Core.Fragments.PageComposition"/> を要求し、得られたバイト列を
/// <see cref="Lithify.Core.Output.IOutputStore"/> に渡す」ことに尽きる。
/// どのノードを再計算するかを決めるのはこの型ではなくグラフである。
/// </para>
/// <para>
/// <see cref="BuildPageAsync"/> が独立して存在できるのは、計算グラフが需要駆動だからである。
/// Hugo や Jekyll のようなプッシュ型（全ソース → 全出力）では「1 ページだけ作る」という
/// 概念自体が成り立たない。R9 のオンデマンド ビルドは追加機能ではなく、
/// 需要駆動を選んだことの帰結である。
/// </para>
/// </remarks>
public interface ISiteBuilder
{
    /// <summary>
    /// サイト全体をビルドする。
    /// </summary>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>ビルドの結果。</returns>
    ValueTask<BuildReport> BuildAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定した 1 ページだけをビルドする。
    /// </summary>
    /// <param name="path">ビルドする出力のパス。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>ビルドの結果。<paramref name="path"/> に対応するページがない場合は空の結果。</returns>
    /// <remarks>
    /// そのページが依存するものだけが遡って評価される。サイドバーのように
    /// サイト横断のインデックスに依存する部分はメタデータのみを読む軽量パスで済むので、
    /// 全記事の本文をパースすることにはならない。
    /// </remarks>
    ValueTask<BuildReport> BuildPageAsync(
        OutputPath path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ソースの変更を計算グラフに反映し、リビジョンを進める。
    /// </summary>
    /// <param name="changed">変更されたコンテンツのパス。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>非同期操作を表すタスク。</returns>
    /// <remarks>
    /// 進行中のバックグラウンド ビルドはここで中断される。古いリビジョンの結果を
    /// キャッシュに書き込ませないため（R9）。
    /// </remarks>
    ValueTask InvalidateAsync(
        IEnumerable<ContentPath> changed,
        CancellationToken cancellationToken = default);
}
