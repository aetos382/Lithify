namespace Lithify.Abstractions;

/// <summary>
/// 形式を跨いで共通の意味を持つメタデータのキー。
/// </summary>
/// <remarks>
/// <para>
/// <c>Lithify.Abstractions</c> はキーの<em>定義</em>のみを持ち、<em>解釈</em>は消費者
/// （<c>Lithify.Blog</c> 等）が行う。<c>Date</c> の書式や <c>Draft</c> の既定値を
/// ここで決めないのは、それらがブログという用途に固有の関心であり、
/// ブログ以外の用途（R2）を縛らないためである。
/// </para>
/// <para>
/// 各パーサーは自形式のネイティブな名前をこれらのキーに写す責務を負う。
/// この写像を <c>Lithify.Blog</c> 側で分岐させると Blog が AsciiDoc の語彙を知る必要が生じ、
/// 差し替え可能性（R4）が崩れる。
/// </para>
/// </remarks>
public static class WellKnownMetadata
{
    /// <summary>
    /// 文書の題名。YAML の <c>title</c>、AsciiDoc の <c>doctitle</c> に対応する。
    /// </summary>
    public static MetadataKey Title { get; } = MetadataKey.Create("title");

    /// <summary>
    /// 文書の日付。YAML の <c>date</c>、AsciiDoc の <c>revdate</c> に対応する。
    /// </summary>
    public static MetadataKey Date { get; } = MetadataKey.Create("date");

    /// <summary>
    /// 文書の最終更新日。
    /// </summary>
    public static MetadataKey LastModified { get; } = MetadataKey.Create("last-modified");

    /// <summary>
    /// 著者。YAML の <c>author</c>、AsciiDoc の <c>author</c> に対応する。
    /// </summary>
    public static MetadataKey Author { get; } = MetadataKey.Create("author");

    /// <summary>
    /// タグ。YAML の <c>tags</c>、AsciiDoc の <c>:page-tags:</c> に対応する。
    /// </summary>
    public static MetadataKey Tags { get; } = MetadataKey.Create("tags");

    /// <summary>
    /// 下書きかどうか。真の場合、既定の出力からは除外される。
    /// </summary>
    public static MetadataKey Draft { get; } = MetadataKey.Create("draft");

    /// <summary>
    /// 出力パスに用いる識別子。省略された場合はファイル名から導出される。
    /// </summary>
    public static MetadataKey Slug { get; } = MetadataKey.Create("slug");

    /// <summary>
    /// 適用するテンプレートの名前。
    /// </summary>
    public static MetadataKey Layout { get; } = MetadataKey.Create("layout");

    /// <summary>
    /// 概要。一覧ページや Feed に用いる。
    /// </summary>
    public static MetadataKey Description { get; } = MetadataKey.Create("description");

    /// <summary>
    /// 文書の言語。
    /// </summary>
    public static MetadataKey Language { get; } = MetadataKey.Create("lang");

    /// <summary>
    /// パーサーが記録する元のコンテンツ形式。
    /// </summary>
    /// <remarks>
    /// 混在サイト（R1）で、テンプレートが元の形式に応じた表示を行えるようにするためのもの。
    /// 値は <see cref="ContentFormat.Value"/> と一致する。
    /// </remarks>
    public static MetadataKey SourceFormat { get; } = MetadataKey.Create("source-format");
}
