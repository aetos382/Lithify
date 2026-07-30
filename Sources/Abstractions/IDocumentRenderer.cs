using System.Collections.Immutable;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

using Lithify.Abstractions.Ast;

namespace Lithify.Abstractions;

/// <summary>
/// 共通 AST を出力形式に変換する。
/// </summary>
/// <remarks>
/// パーサーが形式ごとに存在するのに対し、レンダラーは<em>1 つ</em>である
/// （共通 AST を選んだ主たる動機がここにある）。形式ごとに独立したレンダラーを持つ設計では、
/// 1 サイト内で <c>.md</c> と <c>.adoc</c> を混在させた瞬間に見出し ID の生成規則や
/// 脚注の出力が不揃いになる。
/// </remarks>
public interface IDocumentRenderer
{
    /// <summary>
    /// このレンダラーが生成する内容のメディア型を取得する。
    /// </summary>
    /// <remarks>
    /// HTML レンダラーでは <c>text/html</c>。
    /// </remarks>
    string OutputMediaType { get; }

    /// <summary>
    /// 共通 AST を出力形式に変換する。
    /// </summary>
    /// <param name="document">変換する文書。</param>
    /// <param name="context">レンダリング コンテキスト。</param>
    /// <param name="writer">出力先。</param>
    /// <param name="cancellationToken">取り消しトークン。</param>
    /// <returns>変換の完了を表すタスク。</returns>
    /// <remarks>
    /// 出力先が <see cref="TextWriter"/> であるのは、対象テンプレート エンジン 3 つが
    /// すべて <see cref="TextWriter"/> ベースであり、ここを
    /// <see cref="System.Buffers.IBufferWriter{T}"/> にしてもアダプターで
    /// char から UTF-8 への変換を挟むだけになるためである。
    /// UTF-8 バイト列への境界はフラグメント生成時に一度だけ置かれる。
    /// </remarks>
    ValueTask RenderAsync(
        DocumentNode document,
        IRenderContext context,
        TextWriter writer,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// レンダリング時にレンダラーへ渡される周辺情報。
/// </summary>
/// <remarks>
/// レンダラーが「サイト全体を知っている段階」から受け取るものをここに集約する。
/// レンダラー自身がサイトの構造を知らないようにしておくことで、
/// レンダラーが単体テスト可能なまま保たれる。
/// </remarks>
public interface IRenderContext
{
    /// <summary>
    /// レンダリング対象のコンテンツ パスを取得する。
    /// </summary>
    ContentPath ContentPath { get; }

    /// <summary>
    /// エスケープに用いるエンコーダーを取得する。
    /// </summary>
    /// <remarks>
    /// Fluid が <see cref="TextEncoder"/> を要求するのと同じ型にしてあるので、
    /// レンダラーとテンプレートでエスケープ規則を揃えられる。
    /// </remarks>
    TextEncoder Encoder { get; }

    /// <summary>
    /// コード ブロックに用いるハイライターを取得する。
    /// </summary>
    /// <remarks>
    /// ハイライターが構成されていない場合は
    /// <see cref="PassThroughSyntaxHighlighter.Instance"/> が入る（<see langword="null"/> にはならない）。
    /// </remarks>
    ISyntaxHighlighter SyntaxHighlighter { get; }

    /// <summary>
    /// 外部ファイルを読む経路を取得する。
    /// </summary>
    IContentFileResolver FileResolver { get; }

    /// <summary>
    /// サイト内参照を出力 URL に写す。
    /// </summary>
    /// <param name="target">参照先。</param>
    /// <returns>
    /// 出力 URL。解決できなかった場合は <see langword="null"/>。
    /// </returns>
    /// <remarks>
    /// パーサーはリンクを解決しない（サイト全体を知らないため）。
    /// <see cref="LinkTarget.Internal"/> や <see cref="LinkTarget.Reference"/> から
    /// 実際の URL への写像はここで行われる。
    /// <see langword="null"/> はリンク切れであり、レンダラーは
    /// <see cref="Diagnostic"/> を報告する。
    /// </remarks>
    string? ResolveLink(
        LinkTarget target);

    /// <summary>
    /// レンダリング中に報告された診断を取得する。
    /// </summary>
    ImmutableArray<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// 診断を報告する。
    /// </summary>
    /// <param name="diagnostic">報告する診断。</param>
    void ReportDiagnostic(
        Diagnostic diagnostic);
}
