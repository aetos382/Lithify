using System;
using System.Collections.Immutable;
using System.Text;

using Lithify.Abstractions;
using Lithify.Abstractions.Ast;

using Markdig.Extensions.Footnotes;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax.Inlines;

namespace Lithify.Parsers.Markdig;

/// <summary>
/// Markdig のインラインを共通 AST に写す。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MarkdigBlockMapper"/> と分けているのは、インラインの写しが
/// <see cref="EmphasisInline.DelimiterChar"/> の解釈という独立した関心を含み、
/// かつブロックを組み立てずに検証できるためである。
/// </para>
/// <para>
/// タスク リストの印（<see cref="TaskList"/>）はここでは<em>落とす</em>。
/// Markdig はこれを項目本文の先頭のインラインとして置くが、共通 AST では
/// <see cref="ListItemNode.IsChecked"/> という構造として表すので、
/// 拾うのは <see cref="MarkdigBlockMapper"/> の責務である。
/// </para>
/// </remarks>
internal static class MarkdigInlineMapper
{
    /// <summary>
    /// インラインの並びを写す。
    /// </summary>
    /// <param name="container">写す対象。<see langword="null"/> の場合は空を返す。</param>
    /// <param name="context">写しの文脈。</param>
    /// <returns>写されたインラインの並び。</returns>
    public static ImmutableArray<InlineNode> MapInlines(
        ContainerInline? container,
        MarkdigMappingContext context)
    {
        if (container is null)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<InlineNode>();

        for (var child = container.FirstChild; child is not null; child = child.NextSibling)
        {
            AppendInline(builder, child, context);
        }

        return builder.DrainToImmutable();
    }

    /// <summary>
    /// 1 つのインラインを写して追加する。
    /// </summary>
    /// <param name="builder">追加先。</param>
    /// <param name="inline">写す対象。</param>
    /// <param name="context">写しの文脈。</param>
    /// <remarks>
    /// 写した結果が 0 個になる場合（タスク リストの印）と 1 個になる場合があるので、
    /// 戻り値ではなく追加先を受け取る形にしている。
    /// </remarks>
    private static void AppendInline(
        ImmutableArray<InlineNode>.Builder builder,
        Inline inline,
        MarkdigMappingContext context)
    {
        switch (inline)
        {
            case LiteralInline literal:
                builder.Add(new TextNode(literal.Content.ToString()));
                break;

            case EmphasisInline emphasis:
                builder.Add(new EmphasisNode(
                    MapEmphasisKind(emphasis),
                    MapInlines(emphasis, context)));
                break;

            case CodeInline code:
                builder.Add(new CodeSpanNode(code.Content ?? string.Empty));
                break;

            case LinkInline link:
                builder.Add(MapLink(link, context));
                break;

            case AutolinkInline autolink:
                builder.Add(MapAutolink(autolink, context));
                break;

            case LineBreakInline lineBreak:
                builder.Add(new LineBreakNode(lineBreak.IsHard));
                break;

            case HtmlEntityInline entity:
                // 実体参照は復号済みのテキストとして持つ。生の HTML として残すと
                // レンダラーがエスケープを二重に行うか、まったく行わないかの選択を迫られる。
                // 復号しておけば「テキストは常にエスケープする」規則だけで正しくなる。
                builder.Add(new TextNode(entity.Transcoded.ToString()));
                break;

            case HtmlInline html:
                builder.Add(new RawInlineNode(HtmlFormat, html.Tag ?? string.Empty));
                break;

            case FootnoteLink footnote:
                AppendFootnoteLink(builder, footnote);
                break;

            case TaskList:
                // 構造として ListItemNode.IsChecked に写されるので、ここでは落とす。
                break;

            case ContainerInline container:
                // 種類を判別できないコンテナー。強調でもリンクでもないので、
                // 自身は写さず子だけを平坦に写す。内容が消えるより無印で残るほうがよい。
                for (var child = container.FirstChild; child is not null; child = child.NextSibling)
                {
                    AppendInline(builder, child, context);
                }

                break;

            default:
                // 知らない拡張のインライン。文字列表現があればテキストとして残す。
                // 落とすと本文の一部が黙って消えるので、印字できるものは印字する。
                if (inline.ToString() is { Length: > 0 } text)
                {
                    builder.Add(new TextNode(text));
                }

                break;
        }
    }

    /// <summary>
    /// <c>html</c> という出力形式の名前。
    /// </summary>
    /// <remarks>
    /// <see cref="RawInlineNode.Format"/> と <see cref="RawBlockNode.Format"/> に入れる値。
    /// </remarks>
    internal const string HtmlFormat = "html";

    /// <summary>
    /// 強調の種類を判別する。
    /// </summary>
    /// <param name="emphasis">判別する強調。</param>
    /// <returns>強調の種類。</returns>
    /// <remarks>
    /// <para>
    /// Markdig は記法を <see cref="EmphasisInline.DelimiterChar"/> と
    /// <see cref="EmphasisInline.DelimiterCount"/> の対で持つ。実測した対応は次のとおり。
    /// </para>
    /// <list type="bullet">
    ///   <item><c>*</c> / <c>_</c> は 1 個で <see cref="EmphasisKind.Emphasis"/>、2 個で <see cref="EmphasisKind.Strong"/></item>
    ///   <item><c>~</c> は 2 個で <see cref="EmphasisKind.Strikethrough"/>、1 個で <see cref="EmphasisKind.Subscript"/></item>
    ///   <item><c>^</c> は <see cref="EmphasisKind.Superscript"/>、<c>=</c> は <see cref="EmphasisKind.Mark"/></item>
    ///   <item><c>+</c>（<c>++挿入++</c>）に対応する <see cref="EmphasisKind"/> は無い</item>
    /// </list>
    /// <para>
    /// <c>***a***</c> は入れ子（1 個の中に 2 個）として来るので、
    /// この判別は個々の <see cref="EmphasisInline"/> について閉じている。
    /// </para>
    /// <para>
    /// <c>+</c> を <see cref="EmphasisKind.Emphasis"/> に落としているのは、
    /// 共通 AST に「挿入」が無いためである。
    /// <see cref="Lithify.Markdown.Abstractions.MarkdownOptions"/> は
    /// この記法を有効にする手段を持たないので（<c>Strikethrough</c> だけを個別に有効にする）、
    /// 通常このパスには入らない。
    /// </para>
    /// </remarks>
    private static EmphasisKind MapEmphasisKind(
        EmphasisInline emphasis)
    {
        return emphasis.DelimiterChar switch
        {
            '~' => emphasis.DelimiterCount >= 2 ? EmphasisKind.Strikethrough : EmphasisKind.Subscript,
            '^' => EmphasisKind.Superscript,
            '=' => EmphasisKind.Mark,
            _ => emphasis.DelimiterCount >= 2 ? EmphasisKind.Strong : EmphasisKind.Emphasis,
        };
    }

    /// <summary>
    /// リンクまたは画像を写す。
    /// </summary>
    /// <param name="link">写す対象。</param>
    /// <param name="context">写しの文脈。</param>
    /// <returns>写されたノード。</returns>
    /// <remarks>
    /// 画像の代替テキストは子のインラインを平坦化した文字列にする。
    /// <see cref="ImageNode.AltText"/> が <see cref="string"/> なのは、
    /// 代替テキストが HTML の属性値として出力され、そこに構造を持ち込めないためである。
    /// </remarks>
    private static InlineNode MapLink(
        LinkInline link,
        MarkdigMappingContext context)
    {
        var target = MarkdigLinkTargetMapper.Map(link.Url, context.Path);

        if (target is LinkTarget.Unresolved unresolved)
        {
            context.ReportUnresolvedLink(unresolved.Raw, link);
        }

        var title = string.IsNullOrEmpty(link.Title) ? null : link.Title;

        return link.IsImage
            ? new ImageNode(target, Flatten(link), title)
            : new LinkNode(target, MapInlines(link, context), title);
    }

    /// <summary>
    /// 自動リンクを写す。
    /// </summary>
    /// <param name="autolink">写す対象。</param>
    /// <param name="context">写しの文脈。</param>
    /// <returns>写されたノード。</returns>
    /// <remarks>
    /// <c>&lt;a@b.com&gt;</c> は <see cref="AutolinkInline.IsEmail"/> が真で
    /// <see cref="AutolinkInline.Url"/> にスキームが付かないので、<c>mailto:</c> を補う。
    /// 補わないと相対パスとして分類され、サイト内のファイルを指すことになる。
    /// </remarks>
    private static LinkNode MapAutolink(
        AutolinkInline autolink,
        MarkdigMappingContext context)
    {
        var url = autolink.IsEmail
            ? string.Concat("mailto:", autolink.Url)
            : autolink.Url;

        var target = MarkdigLinkTargetMapper.Map(url, context.Path);

        if (target is LinkTarget.Unresolved unresolved)
        {
            context.ReportUnresolvedLink(unresolved.Raw, autolink);
        }

        // 表示文字列は書かれたままの URL にする（mailto: を補う前のもの）。
        return new LinkNode(target, [new TextNode(autolink.Url ?? string.Empty)], null);
    }

    /// <summary>
    /// 脚注への参照を写して追加する。
    /// </summary>
    /// <param name="builder">追加先。</param>
    /// <param name="link">写す対象。</param>
    /// <remarks>
    /// <para>
    /// Markdig は脚注の定義の末尾に<em>戻りリンク</em>（<see cref="FootnoteLink.IsBackLink"/>）を
    /// 差し込む。これは HTML の出力都合であって文書の構造ではないので落とす。
    /// 残すと共通 AST に「脚注から本文へ戻るリンク」という HTML 固有の概念が漏れる。
    /// </para>
    /// <para>
    /// 識別子は <see cref="Footnote.Label"/>（<c>^1</c> の形）をそのまま使う。
    /// <c>^</c> を剥がさないのは、<see cref="FootnoteDefinitionNode.Id"/> との対応が
    /// 文字列一致で取れていればよく、出力時の見え方はレンダラーが決めるためである。
    /// </para>
    /// </remarks>
    private static void AppendFootnoteLink(
        ImmutableArray<InlineNode>.Builder builder,
        FootnoteLink link)
    {
        if (link.IsBackLink)
        {
            return;
        }

        if (link.Footnote?.Label is { Length: > 0 } label)
        {
            builder.Add(new FootnoteReferenceNode(label));
        }
    }

    /// <summary>
    /// インラインの並びを素の文字列に平坦化する。
    /// </summary>
    /// <param name="container">平坦化する対象。</param>
    /// <returns>テキストのみを連結した文字列。</returns>
    /// <remarks>
    /// 画像の代替テキストの生成に用いる。強調やリンクの記法は失われるが、
    /// 出力先が HTML の属性値なので構造を残す先が無い。
    /// </remarks>
    private static string Flatten(
        ContainerInline container)
    {
        var builder = new StringBuilder();

        Append(builder, container);

        return builder.ToString();
    }

    /// <summary>
    /// <see cref="Flatten"/> の本体。
    /// </summary>
    /// <param name="builder">追加先。</param>
    /// <param name="container">平坦化する対象。</param>
    private static void Append(
        StringBuilder builder,
        ContainerInline container)
    {
        for (var child = container.FirstChild; child is not null; child = child.NextSibling)
        {
            switch (child)
            {
                case LiteralInline literal:
                    builder.Append(literal.Content.AsSpan());
                    break;

                case CodeInline code:
                    builder.Append(code.Content);
                    break;

                case HtmlEntityInline entity:
                    builder.Append(entity.Transcoded.AsSpan());
                    break;

                case LineBreakInline:
                    builder.Append(' ');
                    break;

                case ContainerInline nested:
                    Append(builder, nested);
                    break;

                default:
                    break;
            }
        }
    }
}
