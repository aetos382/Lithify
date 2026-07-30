using System.Collections.Immutable;

using Lithify.Abstractions;
using Lithify.Core.Incremental;

namespace Lithify.Core.Output;

/// <summary>
/// ビルド キャッシュに記録されるノード 1 個分の情報。
/// </summary>
/// <param name="Id">ノードの識別子。</param>
/// <param name="InputFingerprint">評価時の入力を合成したフィンガープリント。</param>
/// <param name="OutputFingerprint">評価結果のフィンガープリント。</param>
/// <param name="Dependencies">評価時に実際に参照した依存先のノード。</param>
/// <remarks>
/// <para>
/// 値そのものは持たない。フラグメントの内容は別の領域に格納し、ここには
/// 「何に依存していて、結果がどのフィンガープリントだったか」だけを持つ。
/// キャッシュのメタデータだけを読み込んで再検証を判断できるようにするためである。
/// </para>
/// <para>
/// <paramref name="Dependencies"/> を記録するのは、次回のビルドで
/// ノードを再計算せずに依存の検証だけを行うためである（early cutoff）。
/// これは宣言ではなく実際に <see cref="IComputeContext.GetAsync{T}"/> が呼ばれた記録なので、
/// 宣言と実使用がずれる種類のバグが起こらない。
/// </para>
/// </remarks>
public sealed record BuildCacheEntry(
    NodeId Id,
    Fingerprint InputFingerprint,
    Fingerprint OutputFingerprint,
    ImmutableArray<NodeId> Dependencies);
