using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Lithify.Abstractions;

namespace Lithify.Core.Output;

/// <summary>
/// 生成物の格納先。ルート付きなので相対の <see cref="OutputPath"/> だけを扱う。
/// </summary>
/// <remarks>
/// <para>
/// I/O の境界である。書き込むべきかどうかの判断は
/// <see cref="OutputDecision.Decide"/> が純粋関数として担い、この型は判断された結果を実行するだけである。
/// </para>
/// <para>
/// 出力ディレクトリの手編集はサポートしない。既存のフィンガープリントはビルド キャッシュの記録から
/// 取得し、実ファイルの内容は読まない。更新日時とサイズでは中身の差し替えを見逃すため中途半端であり、
/// それでいて全出力のハッシュ再計算は live-reload の応答時間と正面衝突する。
/// キャッシュを信頼できない場合の逃げ道は <c>--force</c>（全書き直し）だけである。
/// </para>
/// </remarks>
public interface IOutputStore
{
    /// <summary>
    /// 既存の出力のフィンガープリントを取得する。
    /// </summary>
    /// <param name="path">出力パス。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>既存の出力のフィンガープリント。存在しない場合は <see langword="null"/>。</returns>
    ValueTask<Fingerprint?> TryGetFingerprintAsync(
        OutputPath path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 内容が変わっている場合にのみ出力を書き込む。
    /// </summary>
    /// <param name="path">出力パス。</param>
    /// <param name="content">書き込む内容。断片化したままでよい。</param>
    /// <param name="fingerprint">
    /// <paramref name="content"/> のフィンガープリント。呼び出し側が既に計算しているため受け取る。
    /// </param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>書き込みの結果。</returns>
    /// <remarks>
    /// <para>
    /// <see cref="ReadOnlySequence{T}"/> を受け取るのは、ページがフラグメントの列であって
    /// 連結済みの 1 本のバイト列ではないためである。ここで連結を強制すると、
    /// 内容が変わっていないページの分まで連結の費用を払うことになる。
    /// </para>
    /// <para>
    /// 戻り値の <see cref="WriteOutcome"/> が live-reload の通知源になる。
    /// <see cref="WriteOutcome.Unchanged"/> でないパスの URL だけをブラウザーに通知すればよい。
    /// </para>
    /// </remarks>
    ValueTask<WriteOutcome> WriteAsync(
        OutputPath path,
        ReadOnlySequence<byte> content,
        Fingerprint fingerprint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 出力を削除する。
    /// </summary>
    /// <param name="path">出力パス。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>削除された場合は <see langword="true"/>、存在しなかった場合は <see langword="false"/>。</returns>
    ValueTask<bool> DeleteAsync(
        OutputPath path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 出力を読み取り用に開く。
    /// </summary>
    /// <param name="path">出力パス。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>読み取り用のストリーム。存在しない場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// 開発サーバーが、書き込んだ出力をそのまま読み返して HTTP レスポンスに流すために必要である。
    /// これがないと開発サーバーは <see cref="IOutputStore"/> を迂回する別経路を持つことになり、
    /// 静的ファイル・フィード・ページで扱いが分岐する。
    /// </remarks>
    ValueTask<Stream?> OpenReadAsync(
        OutputPath path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 格納されている出力パスを列挙する。
    /// </summary>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>出力パスの列挙。</returns>
    /// <remarks>
    /// 前回のビルドに存在して今回は生成されなかった出力（削除された記事の成果物）を
    /// 特定するために必要である。
    /// </remarks>
    IAsyncEnumerable<OutputPath> EnumerateAsync(
        CancellationToken cancellationToken = default);
}
