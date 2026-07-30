namespace Lithify.AsciiDoc.Abstractions;

/// <summary>
/// AsciiDoc の文書型。
/// </summary>
/// <remarks>
/// 言語仕様が定める <c>doctype</c> 属性の値であり、セクション構造と
/// <c>leveloffset</c> の意味論を決める。実装が増えても増えるものではないため
/// <see langword="enum"/> でよい。
/// </remarks>
public enum AsciiDocDoctype
{
    /// <summary>
    /// 記事。既定の文書型。最上位のセクションは <c>&lt;h2&gt;</c> に相当する。
    /// </summary>
    Article,

    /// <summary>
    /// 書籍。part / chapter の階層を持つ。
    /// </summary>
    Book,

    /// <summary>
    /// man ページ。<c>NAME</c> / <c>SYNOPSIS</c> セクションの構造が規定される。
    /// </summary>
    Manpage,

    /// <summary>
    /// インライン。ブロック構造を持たず、インライン要素のみとして解釈する。
    /// </summary>
    Inline,
}

/// <summary>
/// 未定義の属性を参照したときの扱い。
/// </summary>
/// <remarks>
/// 言語仕様が定める <c>attribute-missing</c> の値。
/// 記事に書き間違いがあったときサイト全体のビルドを落とすか、
/// 黙って残すか、警告して続けるかを選べる。
/// </remarks>
public enum AttributeMissingBehavior
{
    /// <summary>
    /// 属性参照をそのまま残す。既定の挙動。
    /// </summary>
    Skip,

    /// <summary>
    /// 属性参照を削除する。
    /// </summary>
    Drop,

    /// <summary>
    /// 属性参照を含む行ごと削除する。
    /// </summary>
    DropLine,

    /// <summary>
    /// 属性参照をそのまま残し、警告の診断を報告する。
    /// </summary>
    Warn,
}
