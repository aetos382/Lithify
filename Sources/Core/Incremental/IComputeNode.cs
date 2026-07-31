using System.Threading;
using System.Threading.Tasks;

namespace Lithify.Core.Incremental;

/// <summary>
/// 需要駆動で評価される計算ノード。
/// </summary>
/// <typeparam name="T">計算される値の型。</typeparam>
/// <remarks>
/// <para>
/// 値を要求されるまで計算しない。これが「見ているページだけをビルドする」（R9）の基礎になる。
/// プッシュ型のパイプラインでは「1 ページだけ作る」という概念自体が成り立たない。
/// </para>
/// <para>
/// 実装は次の 3 つを満たさなければならない。
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// <b>べき等性</b> — 同じリビジョンで何度呼ばれても同じ <see cref="Computed{T}"/> を返す。
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>single-flight</b> — 同じノードを複数のスレッドが同時に要求した場合、計算は 1 回だけ行われ、
/// 残りはその完了を待つ。バックグラウンド ビルドと前景のリクエストが衝突するため必須である。
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>リビジョン境界での中断</b> — 評価中に <see cref="IComputeContext.CurrentRevision"/> が進んだ場合、
/// その結果を新しいリビジョンのものとしてキャッシュしてはならない。
/// </description>
/// </item>
/// </list>
/// <para>
/// 依存の宣言は不要である。<see cref="IComputeContext.GetAsync{T}"/> の呼び出しが依存として自動記録される。
/// </para>
/// </remarks>
public interface IComputeNode<T>
{
    /// <summary>
    /// このノードの識別子を取得する。
    /// </summary>
    NodeId Id { get; }

    /// <summary>
    /// このノードの値を取得する。必要ならば再計算される。
    /// </summary>
    /// <param name="context">依存を記録するコンテキスト。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>評価結果。</returns>
    ValueTask<Computed<T>> GetAsync(
        IComputeContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// ノードの評価中に依存関係を記録するコンテキスト。
/// </summary>
/// <remarks>
/// <para>
/// ノードの計算処理は、他のノードの値を直接ではなくこのコンテキスト経由で取得する。
/// これにより「どのノードがどのノードに依存しているか」を、実装が明示的に宣言せずとも
/// グラフが把握できる。宣言と実際の使用がずれる種類のバグが原理的に起こらない。
/// </para>
/// <para>
/// 外部のコンテンツの読み取りも依存として記録される必要があるため、
/// パーサーやテンプレート エンジンは <see cref="Abstractions.IContentResolver"/> を通してのみ
/// コンテンツを読む。その実装はこのコンテキストにコンテンツ ノードを要求する形で構成される。
/// リモートのコンテンツも同じ形で依存になる（取得された時点で入力の一部になるため、
/// 増分ビルドの正しさはリモートの有無に依存しない）。
/// </para>
/// </remarks>
public interface IComputeContext
{
    /// <summary>
    /// 現在のリビジョンを取得する。
    /// </summary>
    /// <remarks>
    /// 評価の途中でこの値が変わることはない。1 回の評価は 1 つのリビジョンに属する。
    /// </remarks>
    Revision CurrentRevision { get; }

    /// <summary>
    /// 依存するノードの値を取得し、その依存関係を記録する。
    /// </summary>
    /// <typeparam name="T">値の型。</typeparam>
    /// <param name="node">依存先のノード。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>依存先の評価結果。</returns>
    ValueTask<Computed<T>> GetAsync<T>(
        IComputeNode<T> node,
        CancellationToken cancellationToken = default);
}
