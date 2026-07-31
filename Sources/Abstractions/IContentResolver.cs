using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Lithify.Abstractions;

/// <summary>
/// パーサーやテンプレート エンジンが外部のコンテンツを読む唯一の経路。
/// </summary>
/// <remarks>
/// <para>
/// プラグインに <see cref="System.IO.File"/> や <c>HttpClient</c> を直接触らせず、
/// この経路に集約する。集約する理由は 2 つあり、どちらも増分ビルドの正しさに関わる。
/// </para>
/// <para>
/// 1 つは<em>依存の記録</em>である。ここを通って読まれたコンテンツは増分計算グラフの
/// 依存として自動的に記録される。これにより include 先の変更が正しく再ビルドを誘発する。
/// この記録があるかどうかが、include を含む文書の増分ビルドが正しく動くかを決める。
/// </para>
/// <para>
/// もう 1 つは<em>再現可能性の分類</em>である。読んだ内容が
/// 「同じアドレスから常に同じ内容が返る」ものかどうかの判定はこの経路の中で行われる。
/// エンジンが自分で通信すると、依存の記録と分類の両方を迂回する。
/// </para>
/// <para>
/// これは<em>ファイル</em>の経路ではない。<see cref="ContentPath"/> はローカル ファイル・
/// リモート URI・メモリ上の内容のいずれも表しうるので、呼び出す側から見て
/// 「ローカルかリモートか」は区別が付かず、区別する必要もない。
/// 実際にどこから読むかは <c>IContentSourceProvider</c> が担う。
/// </para>
/// <para>
/// この型自身は<em>プロバイダを跨ぐ移動の可否を判断する層</em>である。
/// 個々のアドレス空間の中のことはプロバイダが知っているが、
/// 「リモートから取得した文書がローカル ファイルを読もうとしている」ことを
/// 判断できるのはプロバイダ<em>間</em>を見ているこの型だけである。
/// </para>
/// </remarks>
public interface IContentResolver
{
    /// <summary>
    /// 参照元からの相対参照を解決する。
    /// </summary>
    /// <param name="origin">参照元のコンテンツ パス。</param>
    /// <param name="reference">参照の記述。</param>
    /// <param name="resolved">解決されたコンテンツ パス。</param>
    /// <returns>解決できた場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <para>
    /// 解決は 2 段である。まず <paramref name="origin"/> を供給したプロバイダに
    /// <c>IContentSourceProvider.TryResolveReference</c> で解決させ、
    /// 得られた <see cref="ContentPath"/> で<em>改めてプロバイダを選び直す</em>。
    /// 相対参照の解決規則はアドレス空間ごとに違う（ローカルはパス セグメントの結合、
    /// HTTP は RFC 3986、git はリビジョンを引き継ぐ）ため、規則を知るのはプロバイダである。
    /// </para>
    /// <para>
    /// <strong>リモート起点の参照がローカルに解決されることは決してあってはならない。</strong>
    /// 取得した文書がローカル ファイルを読む経路になるためである。逆向き
    /// （ローカル文書からリモートへ）は許される。この判断がこの型の主たる責務である。
    /// </para>
    /// <para>
    /// 解決できない参照は例外ではなく <see langword="false"/> で表す。
    /// 参照の解決失敗は文書の誤りであってプログラムの誤りではないので、
    /// 呼び出し側が <see cref="Diagnostic"/> として報告できるようにする。
    /// </para>
    /// </remarks>
    bool TryResolve(
        ContentPath origin,
        string reference,
        out ContentPath resolved);

    /// <summary>
    /// 指定したコンテンツを開く。
    /// </summary>
    /// <param name="path">開くコンテンツのパス。</param>
    /// <param name="cancellationToken">取り消しトークン。</param>
    /// <returns>開かれたコンテンツ。</returns>
    /// <exception cref="System.IO.FileNotFoundException">
    /// <paramref name="path"/> に対応するコンテンツが存在しない。
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="path"/> を開けるプロバイダが登録されていない。
    /// </exception>
    /// <remarks>
    /// <para>
    /// <c>IContentSourceProvider.CanOpen</c> でプロバイダを選び、そこに委ねる。
    /// <see cref="TryResolve"/> を経ていないパスを渡してもよい
    /// （プラグインが独自に組み立てたパスもこの経路を通れば依存として記録される）。
    /// </para>
    /// <para>
    /// 取得できなかったことは例外で表さない。参照先が存在しないこと（コンテンツの誤り）と
    /// 接続できないこと（環境の誤り）は区別しなければならず、後者でキャッシュを汚しては
    /// ならないので、区別は <c>IContentSourceProvider</c> の結果の分岐で行う。
    /// この型はその分岐を解釈した結果を返す。
    /// </para>
    /// </remarks>
    ValueTask<ContentSource> OpenAsync(
        ContentPath path,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 拡張子からコンテンツ形式への対応。
/// </summary>
/// <remarks>
/// <para>
/// ディスパッチの実際の入力は形式ではなく<em>拡張子</em>なので、
/// この対応を <see cref="IContentParser"/> から分離して利用者が上書きできるようにする。
/// </para>
/// <para>
/// 既定は <c>.md</c> / <c>.markdown</c> → <see cref="ContentFormat.Markdown"/>、
/// <c>.adoc</c> / <c>.asciidoc</c> → <see cref="ContentFormat.AsciiDoc"/>。
/// </para>
/// </remarks>
public interface IContentFormatRegistry
{
    /// <summary>
    /// パスの拡張子からコンテンツ形式を得る。
    /// </summary>
    /// <param name="path">コンテンツのパス。</param>
    /// <param name="format">対応する形式。</param>
    /// <returns>対応が見つかった場合は <see langword="true"/>。</returns>
    bool TryGetFormat(
        ContentPath path,
        out ContentFormat format);

    /// <summary>
    /// 形式を扱えるパーサーを得る。
    /// </summary>
    /// <param name="format">コンテンツ形式。</param>
    /// <param name="parser">対応するパーサー。</param>
    /// <returns>対応が見つかった場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// 同じ形式を複数のパーサーが主張した場合、<em>後から登録されたものが勝つ</em>
    /// （明示的な差し替えを可能にするため）。ただし上書きが起きたことは
    /// <see cref="DiagnosticSeverity.Information"/> で記録され、暗黙に無視されない。
    /// </remarks>
    bool TryGetParser(
        ContentFormat format,
        [MaybeNullWhen(false)] out IContentParser parser);
}
