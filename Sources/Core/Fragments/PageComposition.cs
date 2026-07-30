using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;

using Lithify.Abstractions;
using Lithify.Core.Incremental;

namespace Lithify.Core.Fragments;

/// <summary>
/// ページを構成するフラグメントへの参照。
/// </summary>
/// <param name="Id">フラグメントの識別子。</param>
/// <param name="Node">フラグメントを生成する計算ノード。</param>
/// <remarks>
/// ノードへの参照であって内容ではない。合成の時点でフラグメントを評価するため、
/// 変わっていないフラグメントはキャッシュから返り、再描画されない。
/// </remarks>
public readonly record struct FragmentSlot(
    FragmentId Id,
    IComputeNode<RenderedFragment> Node);

/// <summary>
/// 1 ページ分のフラグメントの並び。
/// </summary>
/// <param name="Path">このページの出力先。</param>
/// <param name="Slots">フラグメントの並び。出力される順序である。</param>
/// <remarks>
/// <para>
/// ページを 1 枚の文字列として作らず、フラグメントの列として持つのが R8 の解である。
/// 本文フラグメントは記事ソースのみに依存し、サイドバーのフラグメントはサイト横断の索引のみに
/// 依存する。新しい記事を追加するとサイドバーのフラグメントだけが再評価され、
/// 各ページの本文はキャッシュから返る。合成はバイト列の連結だけである。
/// </para>
/// <para>
/// スロットの列自体もフィンガープリントを持つ（<see cref="ComputeFingerprint"/>）。
/// フラグメントの内容が同じでも並びが変われば別のページであり、
/// 逆に並びと全フラグメントが同じならば出力は同一なので書き込みを省略できる（R7）。
/// </para>
/// </remarks>
public sealed record PageComposition(
    OutputPath Path,
    ImmutableArray<FragmentSlot> Slots)
{
    /// <summary>
    /// すべてのフラグメントを評価し、このページのフィンガープリントを計算する。
    /// </summary>
    /// <param name="context">依存を記録するコンテキスト。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>評価済みのフラグメントとページ全体のフィンガープリント。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> が <see langword="null"/> である。</exception>
    /// <remarks>
    /// フラグメントの評価は <paramref name="context"/> を経由するので、
    /// このページが依存するフラグメントが自動的に依存として記録される。
    /// </remarks>
    public async ValueTask<ComposedPage> ComposeAsync(
        IComputeContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var fragments = ImmutableArray.CreateBuilder<RenderedFragment>(this.Slots.Length);

        // 逐次に評価する。フラグメントは並行に評価できるが、その並行性はグラフ側が
        // 持つべきものである。ここで Task.WhenAll すると、依存の記録順が非決定的になり、
        // ビルド キャッシュの内容が実行のたびに変わってしまう。
        foreach (var slot in this.Slots)
        {
            var computed = await context.GetAsync(slot.Node, cancellationToken).ConfigureAwait(false);

            fragments.Add(computed.Value);
        }

        var composed = fragments.MoveToImmutable();

        return new ComposedPage(this.Path, composed, ComputeFingerprint(composed));
    }

    /// <summary>
    /// 評価済みのフラグメントからページ全体のフィンガープリントを計算する。
    /// </summary>
    /// <param name="fragments">評価済みのフラグメント。</param>
    /// <returns>ページ全体のフィンガープリント。</returns>
    /// <remarks>
    /// フラグメントの内容ではなくフィンガープリントを合成する。
    /// 全ページの合成のたびに本文のバイト列を読み直すのを避けるためである。
    /// </remarks>
    [Pure]
    public static Fingerprint ComputeFingerprint(
        ImmutableArray<RenderedFragment> fragments)
    {
        var parts = new Fingerprint[fragments.Length];

        for (var i = 0; i < fragments.Length; ++i)
        {
            parts[i] = fragments[i].Fingerprint;
        }

        return Fingerprint.Combine(parts);
    }
}

/// <summary>
/// 合成済みのページ。
/// </summary>
/// <param name="Path">このページの出力先。</param>
/// <param name="Fragments">評価済みのフラグメント。<see cref="PageComposition.Slots"/> と同じ順序である。</param>
/// <param name="Fingerprint">ページ全体のフィンガープリント。</param>
/// <remarks>
/// フラグメントを連結した 1 本のバイト列を持たない。連結は書き込み時に
/// <see cref="Output.IOutputStore"/> へ渡す直前で行えばよく、
/// ここで持つと変わっていないページの分まで連結の費用を払うことになる。
/// </remarks>
public sealed record ComposedPage(
    OutputPath Path,
    ImmutableArray<RenderedFragment> Fragments,
    Fingerprint Fingerprint) :
    IFingerprintable;
