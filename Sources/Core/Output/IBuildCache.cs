using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Lithify.Abstractions;
using Lithify.Core.Incremental;

namespace Lithify.Core.Output;

/// <summary>
/// ビルドを跨いで計算結果と出力の状態を保持するキャッシュ。
/// </summary>
/// <remarks>
/// <para>
/// 増分ビルドがプロセスの再起動を跨いで効くための土台である。
/// これがないと、CLI を起動するたびに全ページの再ビルドが必要になり
/// R6 が「1 回の実行の中でのみ有効」になってしまう。
/// </para>
/// <para>
/// 出力のフィンガープリントを保持するのは <see cref="FileSystemOutputStore"/> が
/// 実ファイルを読まずに R7 の判断を下すためである。
/// </para>
/// </remarks>
public interface IBuildCache
{
    /// <summary>
    /// 前回のビルドで記録された出力のフィンガープリントを取得する。
    /// </summary>
    /// <param name="path">出力パス。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>記録されているフィンガープリント。記録がない場合は <see langword="null"/>。</returns>
    ValueTask<Fingerprint?> TryGetOutputFingerprintAsync(
        OutputPath path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 出力のフィンガープリントを記録する。
    /// </summary>
    /// <param name="path">出力パス。</param>
    /// <param name="fingerprint">フィンガープリント。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>記録の完了を表すタスク。</returns>
    ValueTask SetOutputFingerprintAsync(
        OutputPath path,
        Fingerprint fingerprint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 出力の記録を削除する。
    /// </summary>
    /// <param name="path">出力パス。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>削除の完了を表すタスク。</returns>
    ValueTask RemoveOutputAsync(
        OutputPath path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 記録されている出力パスを列挙する。
    /// </summary>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>出力パスの列挙。</returns>
    IAsyncEnumerable<OutputPath> EnumerateOutputsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ノードの評価結果を取得する。
    /// </summary>
    /// <param name="id">ノードの識別子。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>記録されているエントリー。記録がない場合は <see langword="null"/>。</returns>
    ValueTask<BuildCacheEntry?> TryGetNodeAsync(
        NodeId id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ノードの評価結果を記録する。
    /// </summary>
    /// <param name="entry">記録するエントリー。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>記録の完了を表すタスク。</returns>
    ValueTask SetNodeAsync(
        BuildCacheEntry entry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 記録された内容を永続化する。
    /// </summary>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>永続化の完了を表すタスク。</returns>
    /// <remarks>
    /// ビルドの途中で中断された場合、永続化されないことで「キャッシュが未完了の状態を指す」
    /// 事故を防ぐ。書き込みは原子的でなければならない。
    /// </remarks>
    ValueTask FlushAsync(
        CancellationToken cancellationToken = default);
}
