using System;
using System.Collections.Generic;

namespace Lithify.AsciiDoc.Abstractions;

/// <summary>
/// AsciiDoc の解釈に関する設定。
/// </summary>
/// <remarks>
/// <para>
/// ここに置くのは AsciiDoc という<b>言語仕様</b>が定める語彙だけである。
/// </para>
/// <para>
/// Asciidoctor の <c>safe mode</c> は<b>含めない</b>。あれは言語仕様ではなく Asciidoctor という
/// 実装が「プロセッサに何を許すか」を制御するための API / CLI の概念である。
/// 言語仕様が定めるのは構文・文書構造・属性・プリプロセッサ指令の意味論までである。
/// </para>
/// <para>
/// safe mode が扱っていたセキュリティ上の関心（include を許すか、どのディレクトリの外に
/// 出られないか）は AsciiDoc 固有ではない。同じ関心は Handlebars / Liquid の partial 解決にも
/// 効くし、実際にファイル アクセスを握っているのは Lithify である。
/// したがってそれは <c>Lithify.Abstractions</c> の <c>FileAccessPolicy</c> が担う。
/// </para>
/// </remarks>
public sealed class AsciiDocOptions
{
    /// <summary>
    /// 文書型を取得または設定する。
    /// </summary>
    /// <remarks>
    /// 文書側の <c>:doctype:</c> 属性が指定されていればそれが優先される。
    /// これはサイト全体の既定値である。
    /// </remarks>
    public AsciiDocDoctype Doctype { get; set; } = AsciiDocDoctype.Article;

    /// <summary>
    /// 未定義の属性を参照したときの扱いを取得または設定する。
    /// </summary>
    public AttributeMissingBehavior AttributeMissing { get; set; } = AttributeMissingBehavior.Skip;

    /// <summary>
    /// 段落に対する既定の置換を取得または設定する。
    /// </summary>
    public SubstitutionGroup DefaultSubstitutions { get; set; } = SubstitutionGroup.Normal;

    /// <summary>
    /// サイト全体の既定の document attributes を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 仕様の意味論に従い、文書のヘッダーで同名の属性が定義されていれば文書側が優先される。
    /// ただし名前の末尾に <c>@</c> を付けた場合はこの既定値が優先される（soft set）。
    /// </para>
    /// <para>
    /// 値が <see langword="null"/> の項目は「属性が設定されているが値を持たない」ことを表す
    /// （<c>:toc:</c> に相当）。属性を明示的に未設定にする（<c>:!toc:</c>）には
    /// 項目そのものを含めない。
    /// </para>
    /// </remarks>
    public IDictionary<string, string?> Attributes { get; } =
        new Dictionary<string, string?>(StringComparer.Ordinal);
}
