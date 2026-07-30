using System;

namespace Lithify.AsciiDoc.Abstractions;

/// <summary>
/// AsciiDoc の置換（substitution）グループ。
/// </summary>
/// <remarks>
/// 言語仕様が定める置換の種類。ブロックごとに <c>subs</c> 属性で指定でき、
/// 適用される置換とその順序が決まる。
/// </remarks>
[Flags]
public enum SubstitutionGroup
{
    /// <summary>
    /// 置換を行わない。
    /// </summary>
    None = 0,

    /// <summary>
    /// 特殊文字（<c>&lt;</c> / <c>&gt;</c> / <c>&amp;</c>）の置換。
    /// </summary>
    SpecialCharacters = 1,

    /// <summary>
    /// 引用符・強調などの書式付けマークアップの置換。
    /// </summary>
    Quotes = 2,

    /// <summary>
    /// 属性参照（<c>{name}</c>）の置換。
    /// </summary>
    Attributes = 4,

    /// <summary>
    /// 文字参照・記号（<c>(C)</c> → <c>©</c> 等）の置換。
    /// </summary>
    Replacements = 8,

    /// <summary>
    /// マクロ（<c>image:</c> / <c>link:</c> / <c>xref:</c> 等）の置換。
    /// </summary>
    Macros = 16,

    /// <summary>
    /// 後置の置換（行末の <c>+</c> による強制改行等）。
    /// </summary>
    PostReplacements = 32,

    /// <summary>
    /// 段落に対する既定の置換。すべての置換を含む。
    /// </summary>
    Normal =
        SpecialCharacters | Quotes | Attributes |
        Replacements | Macros | PostReplacements,

    /// <summary>
    /// リテラル ブロック・ソース ブロックに対する既定の置換。
    /// </summary>
    Verbatim = SpecialCharacters,
}
