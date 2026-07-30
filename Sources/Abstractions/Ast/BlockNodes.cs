using System;
using System.Collections.Immutable;

namespace Lithify.Abstractions.Ast;

/// <summary>
/// 文書全体。AST の根。
/// </summary>
/// <param name="Blocks">文書を構成するブロック。</param>
/// <param name="Metadata">文書のメタデータ。</param>
public sealed record DocumentNode(
    ImmutableArray<BlockNode> Blocks,
    DocumentMetadata Metadata) : BlockNode
{
    /// <summary>
    /// 空の文書。
    /// </summary>
    public static DocumentNode Empty { get; } = new([], DocumentMetadata.Empty);
}

/// <summary>
/// 見出しとその配下のブロックからなる節。
/// </summary>
/// <remarks>
/// <para>
/// 見出しを平坦な並びではなく入れ子の木として持つ。AsciiDoc の <c>leveloffset</c> の意味論が
/// 節の入れ子を前提にしており、また目次の生成が木の走査だけで済む。
/// Markdown パーサーは平坦な見出し列からこの木を組み立てる責務を負う。
/// </para>
/// <para>
/// <see cref="AnchorId"/> を <see langword="null"/> 許容にしているのは、
/// アンカーの生成規則をレンダラー側に一元化できるようにするためである。
/// 明示的なアンカー（AsciiDoc の <c>[[id]]</c>、Markdown の <c>{#id}</c>）がある場合のみ
/// パーサーが値を設定する。
/// </para>
/// </remarks>
public sealed record SectionNode : BlockNode
{
    /// <summary>
    /// 見出しレベルの最小値。
    /// </summary>
    public const int MinLevel = 1;

    /// <summary>
    /// 見出しレベルの最大値。HTML の <c>h1</c>–<c>h6</c> に対応する。
    /// </summary>
    public const int MaxLevel = 6;

    /// <summary>
    /// <see cref="SectionNode"/> を生成する。
    /// </summary>
    /// <param name="level">見出しのレベル。<see cref="MinLevel"/> 以上 <see cref="MaxLevel"/> 以下。</param>
    /// <param name="heading">見出しの内容。</param>
    /// <param name="anchorId">見出しに割り当てられたアンカー。</param>
    /// <param name="blocks">この節に属するブロック。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> が範囲外である。</exception>
    public SectionNode(
        int level,
        ImmutableArray<InlineNode> heading,
        string? anchorId,
        ImmutableArray<BlockNode> blocks)
    {
        if (level is < MinLevel or > MaxLevel)
        {
            throw new ArgumentOutOfRangeException(
                nameof(level),
                level,
                Messages.FormatSectionLevelOutOfRange(MinLevel, MaxLevel, level));
        }

        this.Level = level;
        this.Heading = heading;
        this.AnchorId = anchorId;
        this.Blocks = blocks;
    }

    /// <summary>
    /// 見出しのレベルを取得する。
    /// </summary>
    public int Level { get; }

    /// <summary>
    /// 見出しの内容を取得する。
    /// </summary>
    public ImmutableArray<InlineNode> Heading { get; }

    /// <summary>
    /// 見出しに割り当てられたアンカーを取得する。
    /// </summary>
    public string? AnchorId { get; }

    /// <summary>
    /// この節に属するブロックを取得する。
    /// </summary>
    public ImmutableArray<BlockNode> Blocks { get; }
}

/// <summary>
/// 段落。
/// </summary>
/// <param name="Inlines">段落の内容。</param>
public sealed record ParagraphNode(
    ImmutableArray<InlineNode> Inlines) : BlockNode;

/// <summary>
/// コード ブロック。
/// </summary>
/// <param name="Text">コードの本文。</param>
/// <param name="Language">言語識別子。指定がない場合は <see langword="null"/>。</param>
/// <param name="Attributes">形式固有の追加属性（行番号の指定や強調行など）。</param>
/// <remarks>
/// Markdown の fenced code block と AsciiDoc の source block がどちらもこのノードになる。
/// シンタックス ハイライトが形式に依存しないのはこのため。
/// <see cref="Text"/> と <see cref="Language"/> だけでハイライト結果が決まるので、
/// ハイライトはビルドを跨いで、さらに同じスニペットを載せる複数ページ間でメモ化できる。
/// </remarks>
public sealed record CodeBlockNode(
    string Text,
    string? Language,
    ImmutableArray<AttributeEntry> Attributes) : BlockNode;

/// <summary>
/// 箇条書きまたは番号付きリスト。
/// </summary>
/// <param name="Kind">リストの種類。</param>
/// <param name="Start">番号付きリストの開始番号。箇条書きでは意味を持たない。</param>
/// <param name="IsTight">
/// 項目間に段落の間隔を置かないかどうか。CommonMark の tight / loose の区別に対応する。
/// </param>
/// <param name="Items">リストの項目。</param>
public sealed record ListNode(
    ListKind Kind,
    int Start,
    bool IsTight,
    ImmutableArray<ListItemNode> Items) : BlockNode;

/// <summary>
/// リストの項目。
/// </summary>
/// <param name="Blocks">項目の内容。</param>
/// <param name="IsChecked">
/// チェックボックスの状態。チェックボックスを持たない項目では <see langword="null"/>。
/// </param>
public sealed record ListItemNode(
    ImmutableArray<BlockNode> Blocks,
    bool? IsChecked) : BlockNode;

/// <summary>
/// 定義リスト。AsciiDoc の labeled list に対応する。
/// </summary>
/// <param name="Entries">項目。</param>
public sealed record DescriptionListNode(
    ImmutableArray<DescriptionListEntry> Entries) : BlockNode;

/// <summary>
/// 表。
/// </summary>
/// <param name="Columns">列の定義。</param>
/// <param name="Header">見出し行。見出しを持たない表では <see langword="null"/>。</param>
/// <param name="Rows">本体の行。</param>
public sealed record TableNode(
    ImmutableArray<TableColumn> Columns,
    TableRowNode? Header,
    ImmutableArray<TableRowNode> Rows) : BlockNode;

/// <summary>
/// 表の行。
/// </summary>
/// <param name="Cells">セル。</param>
public sealed record TableRowNode(
    ImmutableArray<TableCellNode> Cells) : BlockNode;

/// <summary>
/// 表のセル。
/// </summary>
/// <param name="Blocks">セルの内容。</param>
/// <param name="ColumnSpan">結合する列数。</param>
/// <param name="RowSpan">結合する行数。</param>
/// <remarks>
/// セルの内容をインラインではなくブロックの並びにしているのは、
/// AsciiDoc がセル内に段落やリストを置けるためである。
/// </remarks>
public sealed record TableCellNode(
    ImmutableArray<BlockNode> Blocks,
    int ColumnSpan,
    int RowSpan) : BlockNode;

/// <summary>
/// 引用。
/// </summary>
/// <param name="Blocks">引用の内容。</param>
/// <param name="Attribution">出典。指定がない場合は <see langword="null"/>。</param>
/// <remarks>
/// <see cref="Attribution"/> を持つのは AsciiDoc の quote block が
/// 出典と引用元を構造として持つためである。Markdown パーサーはここを
/// <see langword="null"/> にする。
/// </remarks>
public sealed record BlockQuoteNode(
    ImmutableArray<BlockNode> Blocks,
    Attribution? Attribution) : BlockNode;

/// <summary>
/// 注意喚起。AsciiDoc の <c>NOTE</c> / <c>TIP</c> / <c>WARNING</c> や GitHub の alert に対応する。
/// </summary>
/// <param name="Kind">注意喚起の種類。</param>
/// <param name="Title">見出し。指定がない場合は <see langword="null"/>。</param>
/// <param name="Blocks">内容。</param>
public sealed record AdmonitionNode(
    AdmonitionKind Kind,
    string? Title,
    ImmutableArray<BlockNode> Blocks) : BlockNode;

/// <summary>
/// 脚注の定義。
/// </summary>
/// <param name="Id">脚注の識別子。</param>
/// <param name="Blocks">脚注の内容。</param>
/// <remarks>
/// 定義を文書のブロックとして持ち、本文からは <see cref="FootnoteReferenceNode"/> で参照する。
/// 脚注を出力のどこに置くかはレンダラーの判断に委ねられる。
/// </remarks>
public sealed record FootnoteDefinitionNode(
    string Id,
    ImmutableArray<BlockNode> Blocks) : BlockNode;

/// <summary>
/// 区切り線。
/// </summary>
public sealed record ThematicBreakNode : BlockNode;

/// <summary>
/// 共通 AST で表現できない出力形式固有のブロック。
/// </summary>
/// <param name="Format">この内容が意味を持つ出力形式（<c>html</c> 等）。</param>
/// <param name="Text">そのまま出力される本文。</param>
/// <remarks>
/// 共通 AST に写せない構造の逃げ道。<see cref="Format"/> が一致しないレンダラーは
/// このノードを無視する。エスケープされずに出力されるので、
/// 信頼できない入力をここに入れてはならない。
/// </remarks>
public sealed record RawBlockNode(
    string Format,
    string Text) : BlockNode;

/// <summary>
/// フラグメントの差し込み位置。
/// </summary>
/// <param name="Id">差し込むフラグメントの識別子。</param>
/// <remarks>
/// <para>
/// フラグメント合成（R8）の要。ページを 1 枚の文字列として作らず、
/// 独立に評価されるフラグメントの列として持つための印。
/// </para>
/// <para>
/// 記事本文のフラグメントは記事ソースのみに依存し、サイドバーのフラグメントは
/// サイト横断インデックスのみに依存する。新しい記事を追加したときに
/// サイドバーだけが再評価され、本文はキャッシュから再利用されるのはこの分離による。
/// </para>
/// </remarks>
public sealed record FragmentPlaceholderNode(
    FragmentId Id) : BlockNode;
