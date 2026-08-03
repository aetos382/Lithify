using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

using Lithify.Abstractions;

namespace Lithify.Core.Content;

// DiagnosticIds は Lithify.Core 直下にある。Content と Metadata の両方が使うので、
// どちらか一方の名前空間に置くと他方が「相手の都合の型」を参照することになる。

/// <summary>
/// <see cref="IContentFormatRegistry"/> の既定実装。
/// </summary>
/// <remarks>
/// <para>
/// 登録されたパーサー群を <see cref="IContentParser.SupportedFormats"/> で索引し、
/// 拡張子と媒体型からの写像は <see cref="ContentFormatMap"/> に委ねる。
/// </para>
/// <para>
/// <strong>索引はコンストラクターで確定する。</strong> 後から登録を足せる形にすると、
/// 同じビルドの途中でディスパッチの結果が変わりうることになり、
/// 増分計算グラフのノードの値が「いつ引いたか」に依存する。DI の登録は
/// <c>UseMarkdig()</c> のような拡張メソッドで済んでいるので、
/// この型が可変である必要はない。
/// </para>
/// </remarks>
public sealed class ContentFormatRegistry :
    IContentFormatRegistry
{
    private readonly FrozenDictionary<ContentFormat, IContentParser> _parsers;

    /// <summary>
    /// 指定したパーサー群から <see cref="ContentFormatRegistry"/> を生成する。
    /// </summary>
    /// <param name="parsers">
    /// 登録されたパーサー。同じ形式を複数が主張した場合、<em>列挙の後のもの</em>が勝つ。
    /// </param>
    /// <param name="map">拡張子と媒体型の対応表。<see langword="null"/> の場合は既定を用いる。</param>
    /// <exception cref="ArgumentNullException"><paramref name="parsers"/> が <see langword="null"/> である。</exception>
    /// <remarks>
    /// <para>
    /// DI から <c>IEnumerable&lt;IContentParser&gt;</c> を受ける形にしているので、
    /// 登録順がそのまま優先順になる。<c>Microsoft.Extensions.DependencyInjection</c> は
    /// 複数登録を登録順に列挙するので、「後から <c>Use…()</c> を呼んだほうが勝つ」という
    /// 利用者から見た規則がそのまま成り立つ。
    /// </para>
    /// <para>
    /// パーサーが 0 個でも例外にしない。パーサーを 1 つも登録しないのは構成の誤りだが、
    /// それを言うべきなのはコンテンツを読もうとした時点であって、
    /// 索引を組み立てた時点ではない（<c>clean</c> のようにパースを要しない操作もある）。
    /// </para>
    /// </remarks>
    public ContentFormatRegistry(
        IEnumerable<IContentParser> parsers,
        ContentFormatMap? map = null)
    {
        ArgumentNullException.ThrowIfNull(parsers);

        this.Map = map ?? ContentFormatMap.Default;

        var byFormat = new Dictionary<ContentFormat, IContentParser>();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var parser in parsers)
        {
            ArgumentNullException.ThrowIfNull(parser);

            foreach (var format in parser.SupportedFormats)
            {
                // 空の形式は主張として意味を持たない。パーサー側の誤りだが、
                // 索引に入れると「default の形式を扱えるパーサー」が生まれて
                // 拡張子の対応が無い入力に黙って引っかかる。
                if (format.IsEmpty)
                {
                    diagnostics.Add(new Diagnostic(
                        DiagnosticIds.ParserDeclaredEmptyFormat,
                        DiagnosticSeverity.Warning,
                        Messages.FormatParserDeclaredEmptyFormat(parser.GetType().FullName)));

                    continue;
                }

                // 同じ形式を複数のパーサーが主張した場合は後から登録されたものが勝つ。
                // 差し替えを可能にするための規則である（利用者は組み込みのパーサーの後に
                // 自分のものを登録できる）。診断は出さない。上書きは意図された操作であり、
                // 直さなくても結果は正しい。
                byFormat[format] = parser;
            }
        }

        this._parsers = byFormat.ToFrozenDictionary();
        this.Diagnostics = diagnostics.DrainToImmutable();
    }

    /// <inheritdoc />
    public ImmutableArray<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// 拡張子と媒体型の対応表を取得する。
    /// </summary>
    public ContentFormatMap Map { get; }

    /// <inheritdoc />
    public bool TryGetFormat(
        ContentPath path,
        out ContentFormat format)
    {
        return this.Map.TryGetByExtension(path.Extension, out format);
    }

    /// <summary>
    /// 媒体型からコンテンツ形式を得る。
    /// </summary>
    /// <param name="mediaType">媒体型。引数を含んでいてもよい。</param>
    /// <param name="format">対応する形式。</param>
    /// <returns>対応が見つかった場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <see cref="IContentFormatRegistry"/> には無い経路である。拡張子を持たない
    /// リモート コンテンツのために要る（9.5 の帰結）が、この形で確定させるには
    /// 媒体型を運ぶ手段（<see cref="ContentSourceResult"/> に添えるか、
    /// <see cref="ContentSource"/> が持つか）を先に決める必要がある。
    /// それが決まるまで抽象を広げず、実装側の公開メソッドに留める。
    /// </remarks>
    public bool TryGetFormatByMediaType(
        string? mediaType,
        out ContentFormat format)
    {
        return this.Map.TryGetByMediaType(mediaType, out format);
    }

    /// <inheritdoc />
    public bool TryGetParser(
        ContentFormat format,
        [MaybeNullWhen(false)] out IContentParser parser)
    {
        if (format.IsEmpty)
        {
            parser = null;

            return false;
        }

        return this._parsers.TryGetValue(format, out parser);
    }
}
