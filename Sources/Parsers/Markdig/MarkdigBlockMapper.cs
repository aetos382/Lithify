using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

using Lithify.Abstractions;
using Lithify.Abstractions.Ast;

using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Yaml;
using Markdig.Helpers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Lithify.Parsers.Markdig;

/// <summary>
/// Markdig のブロックを共通 AST に写す。
/// </summary>
/// <remarks>
/// <para>
/// 見出しの扱いがこの写しの中心である。Markdig は見出しを<em>平坦な</em>ブロックの列として持つが、
/// 共通 AST は <see cref="SectionNode"/> の木として持つ。この組み立て
/// （<see cref="BuildSections"/>）は Markdown パーサーの責務として明示されている。
/// </para>
/// <para>
/// フロント マター（<see cref="YamlFrontMatterBlock"/>）は本文には現れない。
/// メタデータとしてすでに読まれており、本文にも出すと二重になる。
/// </para>
/// </remarks>
internal static class MarkdigBlockMapper
{
    /// <summary>
    /// 文書を写す。
    /// </summary>
    /// <param name="document">写す対象。</param>
    /// <param name="metadata">この文書のメタデータ。</param>
    /// <param name="context">写しの文脈。</param>
    /// <returns>写された文書。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="document"/>、<paramref name="metadata"/>、
    /// または <paramref name="context"/> が <see langword="null"/> である。
    /// </exception>
    public static DocumentNode MapDocument(
        MarkdownDocument document,
        DocumentMetadata metadata,
        MarkdigMappingContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(context);

        return new DocumentNode(MapBlocks(document, context), metadata);
    }

    /// <summary>
    /// ブロックの並びを写し、見出しから節の木を組み立てる。
    /// </summary>
    /// <param name="blocks">写す対象。</param>
    /// <param name="context">写しの文脈。</param>
    /// <returns>写されたブロックの並び。</returns>
    private static ImmutableArray<BlockNode> MapBlocks(
        ContainerBlock blocks,
        MarkdigMappingContext context)
    {
        var flat = new List<BlockNode>(blocks.Count);

        foreach (var block in blocks)
        {
            AppendBlock(flat, block, context);
        }

        return BuildSections(flat);
    }

    /// <summary>
    /// 1 つのブロックを写して追加する。
    /// </summary>
    /// <param name="builder">追加先。</param>
    /// <param name="block">写す対象。</param>
    /// <param name="context">写しの文脈。</param>
    /// <remarks>
    /// 写した結果が 0 個になる場合（フロント マター、リンク参照定義）があるので、
    /// 戻り値ではなく追加先を受け取る形にしている。
    /// </remarks>
    private static void AppendBlock(
        List<BlockNode> builder,
        Block block,
        MarkdigMappingContext context)
    {
        switch (block)
        {
            case YamlFrontMatterBlock:
                // メタデータとして既に読まれている。本文には出さない。
                break;

            case HeadingBlock heading:
                // 節の組み立ては後段（BuildSections）が行うので、
                // ここでは見出し 1 つだけを持つ節として置く。
                builder.Add(new SectionNode(
                    Math.Clamp(heading.Level, SectionNode.MinLevel, SectionNode.MaxLevel),
                    MarkdigInlineMapper.MapInlines(heading.Inline, context),
                    GetExplicitAnchorId(heading),
                    []));
                break;

            case ParagraphBlock paragraph:
                builder.Add(new ParagraphNode(
                    MarkdigInlineMapper.MapInlines(paragraph.Inline, context)));
                break;

            case FencedCodeBlock fenced:
                builder.Add(new CodeBlockNode(
                    GetCodeText(fenced),
                    string.IsNullOrEmpty(fenced.Info) ? null : fenced.Info,
                    GetCodeAttributes(fenced)));
                break;

            case CodeBlock code:
                // 字下げによるコード ブロック。言語の指定を持てない。
                builder.Add(new CodeBlockNode(GetCodeText(code), null, []));
                break;

            case ListBlock list:
                builder.Add(MapList(list, context));
                break;

            case QuoteBlock quote:
                // Attribution は null。Markdown の引用は出典を構造として持たない
                // （AsciiDoc の quote block だけが持つ）。
                builder.Add(new BlockQuoteNode(MapBlocks(quote, context), null));
                break;

            case Table table:
                builder.Add(MapTable(table, context));
                break;

            case ThematicBreakBlock:
                builder.Add(new ThematicBreakNode());
                break;

            case HtmlBlock html:
                builder.Add(new RawBlockNode(MarkdigInlineMapper.HtmlFormat, GetLinesText(html.Lines)));
                break;

            case FootnoteGroup group:
                // 脚注の定義はまとめて 1 つのブロックに入っている。
                // 共通 AST では定義それぞれが文書のブロックなので、群を解いて並べる。
                foreach (var child in group)
                {
                    AppendBlock(builder, child, context);
                }

                break;

            case Footnote footnote:
                if (footnote.Label is { Length: > 0 } label)
                {
                    builder.Add(new FootnoteDefinitionNode(label, MapBlocks(footnote, context)));
                }

                break;

            case LinkReferenceDefinitionGroup:
            case LinkReferenceDefinition:
                // 参照定義は本文に現れない。Markdig が既にリンクへ解決している。
                break;

            case ContainerBlock container:
                // 知らない拡張のコンテナー。自身は写せないが、
                // 子は写せるかもしれないので平坦に並べる。
                context.ReportBlockNotRepresentable(container);

                foreach (var child in container)
                {
                    AppendBlock(builder, child, context);
                }

                break;

            default:
                context.ReportBlockNotRepresentable(block);
                break;
        }
    }

    /// <summary>
    /// 平坦な見出しの列から節の木を組み立てる。
    /// </summary>
    /// <param name="flat">見出しを 1 つだけ持つ節を含む、平坦なブロックの並び。</param>
    /// <returns>節が入れ子になったブロックの並び。</returns>
    /// <remarks>
    /// <para>
    /// 各見出しは、それ<em>以降</em>のブロックを、より浅いか同じレベルの見出しが現れるまで配下に取る。
    /// レベルが飛んでいても（<c>h1</c> の次が <c>h3</c>）成立する。
    /// </para>
    /// <para>
    /// <strong>見出しより前のブロックはどの節にも属さない。</strong>
    /// 前書きを最初の節に押し込むと、目次の項目と本文の対応が崩れる。
    /// </para>
    /// <para>
    /// スタックで組み立てるのは、再帰にすると見出しレベルの飛びを扱う分岐が増えるためである。
    /// 深さは <see cref="SectionNode.MaxLevel"/> で抑えられているので、
    /// 入力によってスタックが伸び続けることはない。
    /// </para>
    /// </remarks>
    private static ImmutableArray<BlockNode> BuildSections(
        List<BlockNode> flat)
    {
        var roots = ImmutableArray.CreateBuilder<BlockNode>();

        // 開いている節と、その配下に集めているブロック。浅い順に並ぶ。
        var open = new List<(SectionNode Section, ImmutableArray<BlockNode>.Builder Blocks)>();

        foreach (var node in flat)
        {
            if (node is SectionNode section)
            {
                CloseSectionsTo(section.Level, open, roots);

                open.Add((section, ImmutableArray.CreateBuilder<BlockNode>()));

                continue;
            }

            if (open.Count == 0)
            {
                roots.Add(node);
            }
            else
            {
                open[^1].Blocks.Add(node);
            }
        }

        CloseSectionsTo(SectionNode.MinLevel, open, roots);

        return roots.DrainToImmutable();
    }

    /// <summary>
    /// 指定したレベル以上の深さの節を閉じる。
    /// </summary>
    /// <param name="level">これから開く見出しのレベル。</param>
    /// <param name="open">開いている節。</param>
    /// <param name="roots">どの節にも属さないブロックの追加先。</param>
    /// <remarks>
    /// 閉じた節は 1 つ外側の節の配下に入る。外側が無ければ
    /// <paramref name="roots"/> に入る。閉じるのは深い順（後ろから）でなければ、
    /// 入れ子の関係が崩れる。
    /// </remarks>
    private static void CloseSectionsTo(
        int level,
        List<(SectionNode Section, ImmutableArray<BlockNode>.Builder Blocks)> open,
        ImmutableArray<BlockNode>.Builder roots)
    {
        while (open.Count > 0 && open[^1].Section.Level >= level)
        {
            var (section, blocks) = open[^1];

            open.RemoveAt(open.Count - 1);

            var closed = new SectionNode(
                section.Level,
                section.Heading,
                section.AnchorId,
                blocks.DrainToImmutable());

            if (open.Count == 0)
            {
                roots.Add(closed);
            }
            else
            {
                open[^1].Blocks.Add(closed);
            }
        }
    }

    /// <summary>
    /// 見出しに明示的に書かれたアンカーを取り出す。
    /// </summary>
    /// <param name="heading">対象の見出し。</param>
    /// <returns>書かれていたアンカー。無い場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// <para>
    /// <c>## 見出し {#anchor}</c> の <c>{#anchor}</c> は
    /// <c>UseGenericAttributes()</c> が有効な場合に限り
    /// <see cref="HtmlAttributes.Id"/> として取れる
    /// （<see cref="Lithify.Markdown.Abstractions.MarkdownOptions.Attributes"/>）。
    /// 無効な場合は見出しの文字列の一部になり、ここは常に <see langword="null"/> を返す。
    /// </para>
    /// <para>
    /// <strong><see cref="MarkdigOptions.AutoIdentifiers"/> を有効にすると、
    /// 自動採番された ID もここに現れる。</strong> 明示的なアンカーと区別できなくなるので、
    /// 既定では無効にしてある。
    /// </para>
    /// </remarks>
    private static string? GetExplicitAnchorId(
        HeadingBlock heading)
    {
        return heading.TryGetAttributes()?.Id is { Length: > 0 } id ? id : null;
    }

    /// <summary>
    /// リストを写す。
    /// </summary>
    /// <param name="list">写す対象。</param>
    /// <param name="context">写しの文脈。</param>
    /// <returns>写されたリスト。</returns>
    /// <remarks>
    /// <see cref="ListBlock.IsLoose"/> の否定が <see cref="ListNode.IsTight"/> である。
    /// 開始番号は <see cref="ListItemBlock.Order"/> の最初の項目の値を使う
    /// （<see cref="ListBlock.OrderedStart"/> は文字列で、箇条書きでは空になる）。
    /// </remarks>
    private static ListNode MapList(
        ListBlock list,
        MarkdigMappingContext context)
    {
        var items = ImmutableArray.CreateBuilder<ListItemNode>(list.Count);
        var start = 1;
        var first = true;

        foreach (var child in list)
        {
            if (child is not ListItemBlock item)
            {
                context.ReportBlockNotRepresentable(child);

                continue;
            }

            if (first && list.IsOrdered)
            {
                start = item.Order;
                first = false;
            }

            items.Add(MapListItem(item, context));
        }

        return new ListNode(
            list.IsOrdered ? ListKind.Ordered : ListKind.Unordered,
            start,
            !list.IsLoose,
            items.DrainToImmutable());
    }

    /// <summary>
    /// リストの項目を写す。
    /// </summary>
    /// <param name="item">写す対象。</param>
    /// <param name="context">写しの文脈。</param>
    /// <returns>写された項目。</returns>
    /// <remarks>
    /// タスク リストのチェック状態は、項目の最初の段落の先頭にある
    /// <see cref="TaskList"/> インラインから拾う。この印自体は
    /// <see cref="MarkdigInlineMapper"/> が本文から落とすので、二重には現れない。
    /// </remarks>
    private static ListItemNode MapListItem(
        ListItemBlock item,
        MarkdigMappingContext context)
    {
        return new ListItemNode(MapBlocks(item, context), GetTaskListState(item));
    }

    /// <summary>
    /// 項目のチェック状態を取り出す。
    /// </summary>
    /// <param name="item">対象の項目。</param>
    /// <returns>チェック状態。チェックボックスを持たない項目では <see langword="null"/>。</returns>
    private static bool? GetTaskListState(
        ListItemBlock item)
    {
        if (item.Count == 0 ||
            item[0] is not ParagraphBlock { Inline: { } inline })
        {
            return null;
        }

        return inline.FirstChild is TaskList task ? task.Checked : null;
    }

    /// <summary>
    /// 表を写す。
    /// </summary>
    /// <param name="table">写す対象。</param>
    /// <param name="context">写しの文脈。</param>
    /// <returns>写された表。</returns>
    /// <remarks>
    /// 見出し行は最初の <see cref="TableRow.IsHeader"/> が真の行とする。
    /// GFM の表は見出しを省略できないが、拡張された記法では省略できるため、
    /// 見出しの有無を <see cref="TableNode.Header"/> の <see langword="null"/> で表す。
    /// </remarks>
    private static TableNode MapTable(
        Table table,
        MarkdigMappingContext context)
    {
        var columns = ImmutableArray.CreateBuilder<TableColumn>(table.ColumnDefinitions.Count);

        foreach (var definition in table.ColumnDefinitions)
        {
            columns.Add(new TableColumn(MapAlignment(definition.Alignment), null));
        }

        TableRowNode? header = null;
        var rows = ImmutableArray.CreateBuilder<TableRowNode>(table.Count);

        foreach (var child in table)
        {
            if (child is not TableRow row)
            {
                context.ReportBlockNotRepresentable(child);

                continue;
            }

            var mapped = MapTableRow(row, context);

            if (row.IsHeader && header is null)
            {
                header = mapped;
            }
            else
            {
                rows.Add(mapped);
            }
        }

        return new TableNode(columns.DrainToImmutable(), header, rows.DrainToImmutable());
    }

    /// <summary>
    /// 表の行を写す。
    /// </summary>
    /// <param name="row">写す対象。</param>
    /// <param name="context">写しの文脈。</param>
    /// <returns>写された行。</returns>
    private static TableRowNode MapTableRow(
        TableRow row,
        MarkdigMappingContext context)
    {
        var cells = ImmutableArray.CreateBuilder<TableCellNode>(row.Count);

        foreach (var child in row)
        {
            if (child is not TableCell cell)
            {
                context.ReportBlockNotRepresentable(child);

                continue;
            }

            cells.Add(new TableCellNode(
                MapBlocks(cell, context),
                cell.ColumnSpan,
                cell.RowSpan));
        }

        return new TableRowNode(cells.DrainToImmutable());
    }

    /// <summary>
    /// 列の配置を写す。
    /// </summary>
    /// <param name="alignment">Markdig の配置。</param>
    /// <returns>共通 AST の配置。</returns>
    private static ColumnAlignment MapAlignment(
        TableColumnAlign? alignment)
    {
        return alignment switch
        {
            TableColumnAlign.Left => ColumnAlignment.Left,
            TableColumnAlign.Center => ColumnAlignment.Center,
            TableColumnAlign.Right => ColumnAlignment.Right,
            _ => ColumnAlignment.None,
        };
    }

    /// <summary>
    /// コード ブロックの属性を取り出す。
    /// </summary>
    /// <param name="fenced">対象のコード ブロック。</param>
    /// <returns>属性の並び。</returns>
    /// <remarks>
    /// <c>```cs linenums</c> の <c>linenums</c> のように、言語識別子の後に書かれた文字列を
    /// <c>arguments</c> という名前の属性として持つ。解釈しないのは、
    /// これがコード ブロックの記法として標準化されておらず、
    /// 意味を与えるのはレンダラー（とその設定）だからである。
    /// </remarks>
    private static ImmutableArray<AttributeEntry> GetCodeAttributes(
        FencedCodeBlock fenced)
    {
        return string.IsNullOrEmpty(fenced.Arguments)
            ? []
            : [new AttributeEntry(CodeArgumentsAttribute, fenced.Arguments)];
    }

    /// <summary>
    /// フェンスの情報行の、言語識別子より後の部分を保持する属性の名前。
    /// </summary>
    internal const string CodeArgumentsAttribute = "arguments";

    /// <summary>
    /// コード ブロックの本文を取り出す。
    /// </summary>
    /// <param name="code">対象のコード ブロック。</param>
    /// <returns>コードの本文。</returns>
    private static string GetCodeText(
        CodeBlock code)
    {
        return GetLinesText(code.Lines);
    }

    /// <summary>
    /// 行の集まりを 1 つの文字列に連結する。
    /// </summary>
    /// <param name="lines">連結する行。</param>
    /// <returns>改行で連結された文字列。</returns>
    /// <remarks>
    /// <para>
    /// 改行は <c>\n</c> に揃える。元のファイルが CRLF でも、コードの本文が
    /// 環境によって変わると同じ内容のコード ブロックが別のフィンガープリントになり、
    /// シンタックス ハイライトのメモ化（<see cref="CodeBlockNode"/> の注記）が効かなくなる。
    /// </para>
    /// <para>
    /// 末尾に改行は付けない。付けるかどうかは出力側の判断であり、
    /// <c>Lines</c> は改行を含まない行の並びとして来るので、
    /// ここで付けると「本文が空のコード ブロック」が改行 1 つを持つことになる。
    /// </para>
    /// </remarks>
    private static string GetLinesText(
        StringLineGroup lines)
    {
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        for (var i = 0; i < lines.Count; ++i)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }

            builder.Append(lines.Lines[i].Slice.AsSpan());
        }

        return builder.ToString();
    }
}
