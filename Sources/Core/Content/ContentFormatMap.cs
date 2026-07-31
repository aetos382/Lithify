using System;
using System.Collections.Frozen;
using System.Collections.Generic;

using JetBrains.Annotations;

using Lithify.Abstractions;

namespace Lithify.Core.Content;

/// <summary>
/// 拡張子および媒体型から <see cref="ContentFormat"/> への対応表。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ContentFormatRegistry"/> から分離しているのは、対応表が<em>純粋な値</em>であり、
/// パーサーの登録（実行時の DI の状態）とは別の寿命を持つためである。
/// 既定に対する差分を組み立てる操作をテストするのに、パーサーを 1 つも要らない。
/// </para>
/// <para>
/// <strong>この表は構成ファイルから束縛しない。</strong>
/// <see cref="ContentFormat"/> の名前を組み立てるのはコードだけである
/// （<see cref="ContentFormat"/> の注記を参照）。構成に形式名を書けるようにすると
/// 利用者が名前の表記を選ぶことになり、揺れを吸収する義務が生じる。
/// 利用者との接触面はファイルの拡張子であって形式名ではない。
/// </para>
/// </remarks>
public sealed class ContentFormatMap
{
    private readonly FrozenDictionary<string, ContentFormat> _extensions;

    private readonly FrozenDictionary<string, ContentFormat> _mediaTypes;

    // span で引くための投影を作り置きする。GetAlternateLookup は比較子が
    // ReadOnlySpan<char> を受け付けるかを毎回検査するので、引くたびに呼ばない。
    private readonly FrozenDictionary<string, ContentFormat>.AlternateLookup<ReadOnlySpan<char>> _extensionLookup;

    private readonly FrozenDictionary<string, ContentFormat>.AlternateLookup<ReadOnlySpan<char>> _mediaTypeLookup;

    private ContentFormatMap(
        FrozenDictionary<string, ContentFormat> extensions,
        FrozenDictionary<string, ContentFormat> mediaTypes)
    {
        this._extensions = extensions;
        this._mediaTypes = mediaTypes;
        this._extensionLookup = extensions.GetAlternateLookup<ReadOnlySpan<char>>();
        this._mediaTypeLookup = mediaTypes.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    /// <summary>
    /// 既定の対応表を取得する。
    /// </summary>
    /// <remarks>
    /// <c>.md</c> / <c>.markdown</c> → <see cref="ContentFormat.Markdown"/>、
    /// <c>.adoc</c> / <c>.asciidoc</c> → <see cref="ContentFormat.AsciiDoc"/>、
    /// <c>.html</c> / <c>.htm</c> → <see cref="ContentFormat.Html"/>。
    /// 対応する形式を扱うパーサーが登録されているかどうかとは無関係である
    /// （表は「どの形式として読むか」だけを言い、読めるかどうかは登録側の話である）。
    /// </remarks>
    public static ContentFormatMap Default { get; } = new(
        new Dictionary<string, ContentFormat>(StringComparer.OrdinalIgnoreCase)
        {
            [".md"] = ContentFormat.Markdown,
            [".markdown"] = ContentFormat.Markdown,
            [".adoc"] = ContentFormat.AsciiDoc,
            [".asciidoc"] = ContentFormat.AsciiDoc,
            [".html"] = ContentFormat.Html,
            [".htm"] = ContentFormat.Html,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, ContentFormat>(StringComparer.OrdinalIgnoreCase)
        {
            // text/markdown は RFC 7763 で登録されている。AsciiDoc には登録された媒体型がなく、
            // text/x-asciidoc が事実上の慣行なので両方を引く。
            ["text/markdown"] = ContentFormat.Markdown,
            ["text/x-markdown"] = ContentFormat.Markdown,
            ["text/asciidoc"] = ContentFormat.AsciiDoc,
            ["text/x-asciidoc"] = ContentFormat.AsciiDoc,
            ["text/html"] = ContentFormat.Html,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// 拡張子の対応を追加または上書きした対応表を返す。
    /// </summary>
    /// <param name="extension">拡張子。先頭の <c>.</c> は省略できる。</param>
    /// <param name="format">対応する形式。</param>
    /// <returns>新しい対応表。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="extension"/> が <see langword="null"/> である。</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="extension"/> が空である、または <paramref name="format"/> が
    /// <see langword="default"/> である。
    /// </exception>
    [Pure]
    public ContentFormatMap WithExtension(
        string extension,
        ContentFormat format)
    {
        ArgumentException.ThrowIfNullOrEmpty(extension);

        if (format.IsEmpty)
        {
            throw new ArgumentException(Messages.ContentFormatMustNotBeEmpty, nameof(format));
        }

        return new ContentFormatMap(
            With(this._extensions, NormalizeExtension(extension), format),
            this._mediaTypes);
    }

    /// <summary>
    /// 媒体型の対応を追加または上書きした対応表を返す。
    /// </summary>
    /// <param name="mediaType">媒体型（<c>text/markdown</c>）。引数は省略する。</param>
    /// <param name="format">対応する形式。</param>
    /// <returns>新しい対応表。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mediaType"/> が <see langword="null"/> である。</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="mediaType"/> が空である、または <paramref name="format"/> が
    /// <see langword="default"/> である。
    /// </exception>
    [Pure]
    public ContentFormatMap WithMediaType(
        string mediaType,
        ContentFormat format)
    {
        ArgumentException.ThrowIfNullOrEmpty(mediaType);

        if (format.IsEmpty)
        {
            throw new ArgumentException(Messages.ContentFormatMustNotBeEmpty, nameof(format));
        }

        return new ContentFormatMap(
            this._extensions,
            With(this._mediaTypes, NormalizeMediaType(mediaType).ToString(), format));
    }

    /// <summary>
    /// 拡張子から形式を引く。
    /// </summary>
    /// <param name="extension">拡張子（先頭の <c>.</c> を含む）。</param>
    /// <param name="format">対応する形式。</param>
    /// <returns>対応が見つかった場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <para>
    /// <see cref="ContentPath.Extension"/> がそのまま渡せるよう
    /// <see cref="ReadOnlySpan{T}"/> で受ける。文字列を作らずに引けるので、
    /// 全入力の列挙で拡張子ごとに割り当てが起きない。
    /// </para>
    /// <para>
    /// <strong>比較は大文字小文字を区別しない。</strong> <see cref="ContentPath"/> が
    /// 区別するのは、出力される URL が区別するからである。しかし拡張子は
    /// <see cref="ContentPath.WithExtension"/> で <c>.html</c> に置き換えられて消えるので、
    /// ここで区別しなくても環境によって出力が変わることはない。区別すると
    /// Windows で <c>.MD</c> になったファイルが黙ってコンテンツでなくなる。
    /// </para>
    /// </remarks>
    public bool TryGetByExtension(
        ReadOnlySpan<char> extension,
        out ContentFormat format)
    {
        // 空の span を辞書に問い合わせない。拡張子を持たないパスは日常的にあり
        // （README、リモートのディレクトリ URI）、それを対応の欠落として扱えば済む。
        if (extension.IsEmpty)
        {
            format = default;

            return false;
        }

        return this._extensionLookup.TryGetValue(extension, out format);
    }

    /// <summary>
    /// 媒体型から形式を引く。
    /// </summary>
    /// <param name="mediaType">媒体型。引数（<c>; charset=utf-8</c>）を含んでいてもよい。</param>
    /// <param name="format">対応する形式。</param>
    /// <returns>対応が見つかった場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <para>
    /// 拡張子を持たないリモート コンテンツのための経路である
    /// （<c>https://example.com/a/</c> や、拡張子なしで配信される生の Markdown）。
    /// </para>
    /// <para>
    /// 引数を落として型と副型だけで引く。<c>text/markdown; variant=GFM</c> の
    /// <c>variant</c> は方言の指定であって形式の区別ではないので、
    /// どのパーサーに渡すかの判断には影響しない（方言は
    /// <c>MarkdownOptions.Flavor</c> のように構成側で決まる）。
    /// </para>
    /// </remarks>
    public bool TryGetByMediaType(
        string? mediaType,
        out ContentFormat format)
    {
        if (string.IsNullOrEmpty(mediaType))
        {
            format = default;

            return false;
        }

        return this._mediaTypeLookup.TryGetValue(NormalizeMediaType(mediaType), out format);
    }

    /// <summary>
    /// 拡張子を先頭の <c>.</c> を持つ形に揃える。
    /// </summary>
    /// <param name="extension">拡張子。</param>
    /// <returns>正規化された拡張子。</returns>
    /// <remarks>
    /// 引く側が渡すのは <see cref="ContentPath.Extension"/> なので必ず <c>.</c> で始まる。
    /// 表を組み立てる側が <c>"md"</c> と書いたときに黙って引けなくなるのを避ける。
    /// </remarks>
    private static string NormalizeExtension(
        string extension)
    {
        return extension[0] == '.'
            ? extension
            : string.Concat(".", extension);
    }

    /// <summary>
    /// 媒体型から引数を落とし、前後の空白を取り除く。
    /// </summary>
    /// <param name="mediaType">媒体型。</param>
    /// <returns>型と副型のみの部分。</returns>
    private static ReadOnlySpan<char> NormalizeMediaType(
        string mediaType)
    {
        var span = mediaType.AsSpan();
        var separator = span.IndexOf(';');

        return (separator >= 0 ? span[..separator] : span).Trim();
    }

    /// <summary>
    /// 対応を 1 つ追加または上書きした辞書を作る。
    /// </summary>
    /// <param name="source">元の辞書。</param>
    /// <param name="key">鍵。</param>
    /// <param name="format">値。</param>
    /// <returns>新しい辞書。</returns>
    private static FrozenDictionary<string, ContentFormat> With(
        FrozenDictionary<string, ContentFormat> source,
        string key,
        ContentFormat format)
    {
        var entries = new Dictionary<string, ContentFormat>(source, StringComparer.OrdinalIgnoreCase)
        {
            [key] = format,
        };

        return entries.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
