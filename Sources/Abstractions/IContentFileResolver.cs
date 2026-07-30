using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Lithify.Abstractions;

/// <summary>
/// パーサーやテンプレート エンジンが外部ファイルを読む唯一の経路。
/// </summary>
/// <remarks>
/// <para>
/// 実装は <see cref="FileAccessPolicy"/> を適用する。プラグインに
/// <see cref="System.IO.File"/> を直接触らせず、この経路に集約することで
/// ポリシーの適用漏れを型で防ぐ。
/// </para>
/// <para>
/// 読み取ったファイルは増分計算グラフの<em>依存として自動的に記録される</em>。
/// これにより include 先の変更が正しく再ビルドを誘発する。
/// この記録があるかどうかが、include を含む文書の増分ビルドが正しく動くかを決める。
/// </para>
/// </remarks>
public interface IContentFileResolver
{
    /// <summary>
    /// 参照元からの相対参照を解決する。
    /// </summary>
    /// <param name="origin">参照元のコンテンツ パス。</param>
    /// <param name="reference">参照の記述。</param>
    /// <param name="resolved">解決されたコンテンツ パス。</param>
    /// <returns>解決できた場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// ポリシーに反する参照は例外ではなく <see langword="false"/> で表す。
    /// 参照の解決失敗は文書の誤りであってプログラムの誤りではないので、
    /// 呼び出し側が <see cref="Diagnostic"/> として報告できるようにする。
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
    /// <paramref name="path"/> に対応するファイルが存在しない。
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// <see cref="FileAccessPolicy"/> がこのアクセスを許可していない。
    /// </exception>
    /// <remarks>
    /// <see cref="TryResolve"/> を経ていないパスを渡してもポリシーは適用される。
    /// 二重に検査するのは、プラグインが独自に組み立てたパスを渡す経路を塞ぐため。
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
