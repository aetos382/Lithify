using System.Threading;
using System.Threading.Tasks;

namespace Lithify.Abstractions;

/// <summary>
/// 1 つのアドレス空間からコンテンツを取得する手段。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IContentResolver"/> がプロバイダを選び、実際の取得をここに委ねる。
/// ローカル ファイル システムもこの抽象の 1 実装であり、特別扱いされない
/// （<em>ローカルが特殊例になる</em>のがこの抽象を引いたことの主な見返りである）。
/// </para>
/// <para>
/// <strong>プロバイダは自前の永続ストアを必要とする。</strong>
/// <see cref="ContentSourceResult.Unchanged"/> を返すには、検証子・鮮度の期限・
/// <em>内容そのもの</em>をビルドを跨いで保持していなければならない。下流がキャッシュ済みなら
/// 内容は要らないが、下流のキャッシュだけが失われた場合（キャッシュ ディレクトリの一部欠落、
/// ノードの追加）に再取得できる必要がある。ゆえにビルド キャッシュとは別に、
/// プロバイダが名前空間を分けて使える永続領域を渡す。
/// </para>
/// <para>
/// <strong>時刻は <c>TimeProvider</c> から取る。</strong> 鮮度の判定は時刻に依存するので、
/// <c>DateTimeOffset.UtcNow</c> を直に読むとキャッシュの挙動をテストできない。
/// </para>
/// <para>
/// <strong>絶対アドレス化の最後の 1 段はこの内側で行う。</strong>
/// <see cref="ContentPath"/> から実際に開けるアドレスへの変換
/// （ローカルならサイト ルートと結合した完全修飾パス、HTTP なら絶対 URI）は
/// プロバイダの内部に留め、外に出さない。
/// </para>
/// </remarks>
public interface IContentSourceProvider
{
    /// <summary>
    /// このプロバイダの識別子を取得する。
    /// </summary>
    /// <remarks>
    /// <see cref="SourceValidator.ProviderId"/> に入る値である。
    /// 他のプロバイダのトークンを誤解釈しないために必要なので、
    /// <em>実装の同一性ではなく取得手段の同一性</em>を表すよう、実行ごとに安定した値にする。
    /// </remarks>
    string Id { get; }

    /// <summary>
    /// このプロバイダが指定したパスを開けるかどうかを判定する。
    /// </summary>
    /// <param name="path">判定するパス。</param>
    /// <returns>開ける場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <strong>プロバイダはパスの関数である。</strong> ゆえに <see cref="ContentPath"/> に
    /// プロバイダの識別子を持たせてはならない（パスを作るのにプロバイダが必要になり循環する）。
    /// </remarks>
    bool CanOpen(
        ContentPath path);

    /// <summary>
    /// 指定したコンテンツを取得する。
    /// </summary>
    /// <param name="path">取得するコンテンツのパス。</param>
    /// <param name="previous">前回の検証子。初回は <see langword="null"/>。</param>
    /// <param name="cancellationToken">取り消しトークン。</param>
    /// <returns>取得の結果。</returns>
    /// <remarks>
    /// <para>
    /// <strong>常に呼ばれる。</strong> 通信するかどうか、鮮度が切れているかどうかは
    /// プロバイダが内部で決める（<see cref="ContentSourceResult.Unchanged"/> 参照）。
    /// 呼び出し側が鮮度を判断して呼ぶかどうかを決める設計にしてはならない。
    /// 鮮度の概念が抽象に漏れる。
    /// </para>
    /// <para>
    /// 取得の失敗を例外で表さないのは、参照先が存在しないこと（コンテンツの誤り）と
    /// 到達できないこと（環境の誤り）を呼び出し側が区別できなければならないためである。
    /// </para>
    /// </remarks>
    ValueTask<ContentSourceResult> OpenAsync(
        ContentPath path,
        SourceValidator? previous,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// このアドレス空間の規則に従って相対参照を解決する。
    /// </summary>
    /// <param name="origin">参照を書いた文書の位置。このプロバイダが開けるパスである。</param>
    /// <param name="reference">文書に書かれたままの参照の記述。</param>
    /// <param name="resolved">解決されたパス。</param>
    /// <returns>解決できた場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <para>
    /// <strong>解決規則はアドレス空間ごとに違うので、ここが正しい置き場所である。</strong>
    /// ローカルはパス セグメントの結合、HTTP は RFC 3986 の相対参照解決
    /// （基準が末尾の <c>/</c> の有無で変わる）、git はリポジトリ内のパス結合に加えて
    /// <em>リビジョンの引き継ぎ</em>（起点が SHA 固定なら参照先も同じ SHA）、
    /// インメモリは authority を引き継いだ名前の結合。
    /// 中核がこれらを全部知るのは「中核はスキームを特別扱いしない」という方針と矛盾する。
    /// </para>
    /// <para>
    /// <strong>解決結果が自分の開けるパスである必要はない。</strong>
    /// ローカル文書に絶対 URI が書かれればリモート プロバイダの領域に移る。
    /// プロバイダを跨ぐ移動の可否を判断するのは <see cref="IContentResolver"/> であり、
    /// ここではしない（プロバイダはプロバイダ間の関係を見ていないため）。
    /// </para>
    /// </remarks>
    bool TryResolveReference(
        ContentPath origin,
        string reference,
        out ContentPath resolved);
}
