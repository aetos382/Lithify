using Lithify.Abstractions;

namespace Lithify.Core.Incremental;

/// <summary>
/// 計算ノードの評価結果。
/// </summary>
/// <typeparam name="T">値の型。</typeparam>
/// <param name="Value">計算された値。</param>
/// <param name="Fingerprint">値の内容から決まるフィンガープリント。</param>
/// <param name="ChangedAt">値が最後に変化したリビジョン。</param>
/// <remarks>
/// <para>
/// <paramref name="ChangedAt"/> が early cutoff の要である。ノードを再計算しても
/// <paramref name="Fingerprint"/> が前回と一致した場合、<paramref name="ChangedAt"/> は
/// 更新せず据え置く。下流のノードは依存の <paramref name="ChangedAt"/> が自分の検証済み
/// リビジョンより新しいかどうかだけを見るので、これにより下流の再計算が起きない。
/// </para>
/// <para>
/// 「検証したリビジョン」を持たないのは、それがノード自身の状態であって
/// 評価結果の一部ではないためである。<see cref="Computed{T}"/> は
/// ビルド キャッシュに保存できる純粋な値である。
/// </para>
/// </remarks>
public readonly record struct Computed<T>(
    T Value,
    Fingerprint Fingerprint,
    Revision ChangedAt);
