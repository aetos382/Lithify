using System;
using System.Collections.Immutable;

using Lithify.Abstractions;

using Markdig.Syntax;

namespace Lithify.Parsers.Markdig;

/// <summary>
/// Markdig の AST から共通 AST への写しの間、写し全体で共有される状態。
/// </summary>
/// <remarks>
/// <para>
/// 診断の収集先と、リンクの分類に要る文書のパスを運ぶ。写しの各段
/// （<see cref="MarkdigBlockMapper"/> / <see cref="MarkdigInlineMapper"/>）に
/// 引数として引き回すのは、写し自体を状態を持たない <see langword="static"/> な関数の集まりに
/// 保つためである。写しがインスタンスの状態を持つと、同じパーサーで複数の文書を
/// 並行に処理できなくなる。
/// </para>
/// <para>
/// <strong>位置情報の 0 起算と 1 起算の変換はここに一元化する。</strong>
/// Markdig の <see cref="MarkdownObject.Line"/> と <see cref="MarkdownObject.Column"/> は
/// 0 起算だが、<see cref="SourceLocation"/> は 1 起算である
/// （<c>0</c> が「位置不明」を意味するため）。各写しで <c>+ 1</c> を書くと
/// 1 箇所忘れるだけで診断の行番号が 1 行ずれる。
/// </para>
/// </remarks>
internal sealed class MarkdigMappingContext
{
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics;

    /// <summary>
    /// <see cref="MarkdigMappingContext"/> を生成する。
    /// </summary>
    /// <param name="path">写している文書のパス。</param>
    /// <param name="diagnostics">診断の収集先。</param>
    /// <exception cref="ArgumentNullException"><paramref name="diagnostics"/> が <see langword="null"/> である。</exception>
    public MarkdigMappingContext(
        ContentPath path,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        this.Path = path;
        this._diagnostics = diagnostics;
    }

    /// <summary>
    /// 写している文書のパスを取得する。
    /// </summary>
    /// <remarks>
    /// リンクの相対参照の基準であり、診断の宛先でもある。
    /// </remarks>
    public ContentPath Path { get; }

    /// <summary>
    /// 解決できなかったリンクを報告する。
    /// </summary>
    /// <param name="raw">元の記述。</param>
    /// <param name="origin">この記述が現れた位置を持つ Markdig のオブジェクト。</param>
    /// <exception cref="ArgumentNullException"><paramref name="origin"/> が <see langword="null"/> である。</exception>
    /// <remarks>
    /// <para>
    /// 重大度は警告である。リンクが解決できないのは誤りだが、それが
    /// 「サイト ルートの外を指している」なのか「まだ書いていないページを指している」なのかを
    /// パーサーは区別できない（サイト全体を知らない）。エラーにするのは
    /// サイト全体を知っている段階の判断である。
    /// </para>
    /// <para>
    /// なお <see cref="Lithify.Abstractions.Ast.LinkTarget.Internal"/> に分類できても
    /// 参照先が実在するとは限らない。実在の確認はここでは行わない。
    /// </para>
    /// </remarks>
    public void ReportUnresolvedLink(
        string raw,
        MarkdownObject origin)
    {
        ArgumentNullException.ThrowIfNull(origin);

        this._diagnostics.Add(new Diagnostic(
            DiagnosticIds.LinkTargetNotResolvable,
            DiagnosticSeverity.Warning,
            Messages.FormatLinkTargetNotResolvable(raw),
            this.Path,
            Locate(origin)));
    }

    /// <summary>
    /// 共通 AST に写せなかったブロックを報告する。
    /// </summary>
    /// <param name="block">写せなかったブロック。</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> が <see langword="null"/> である。</exception>
    /// <remarks>
    /// 内容は落ちる。落とすこと自体を黙って行わないための診断である。
    /// </remarks>
    public void ReportBlockNotRepresentable(
        Block block)
    {
        ArgumentNullException.ThrowIfNull(block);

        this._diagnostics.Add(new Diagnostic(
            DiagnosticIds.BlockNotRepresentable,
            DiagnosticSeverity.Warning,
            Messages.FormatBlockNotRepresentable(block.GetType().Name),
            this.Path,
            Locate(block)));
    }

    /// <summary>
    /// Markdig のオブジェクトの位置を <see cref="SourceLocation"/> に直す。
    /// </summary>
    /// <param name="obj">対象のオブジェクト。</param>
    /// <returns>1 起算の位置。</returns>
    /// <remarks>
    /// <para>
    /// Markdig は 0 起算、<see cref="SourceLocation"/> は 1 起算なので 1 を足す。
    /// </para>
    /// <para>
    /// インラインの位置は <c>UsePreciseSourceLocation()</c> を有効にしても
    /// <c>0, 0</c> のままになることがある（<see cref="global::Markdig.Syntax.Inlines.ContainerInline"/> や、
    /// 自動リンクの拡張が作るインラインで実測した）。その場合は
    /// 1 を足すと<em>1 行 1 桁という誤った位置</em>になるので、不明のまま返す。
    /// 位置不明の診断はファイル名だけを示すが、誤った行を示すより害が小さい。
    /// </para>
    /// </remarks>
    public static SourceLocation Locate(
        MarkdownObject obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        return obj.Line == 0 && obj.Column == 0
            ? default
            : new SourceLocation(obj.Line + 1, obj.Column + 1);
    }
}
