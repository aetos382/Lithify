using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;

using Lithify.Abstractions;

using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Lithify.Parsers.Markdig;

/// <summary>
/// YAML フロント マターを <see cref="DocumentMetadata"/> に写す。
/// </summary>
/// <remarks>
/// <para>
/// YamlDotNet への依存はこのパッケージに閉じる。<see cref="MetadataValue"/> は
/// YAML の型体系を持たない（スカラーは文字列のまま）ので、
/// 日付や数値の解釈は消費者に委ねられる。
/// </para>
/// <para>
/// <strong>誤りは例外ではなく診断として返す。</strong> フロント マターが壊れていても
/// 本文はレンダリングできるし、1 つの記事の YAML の誤りでサイト全体のビルドが
/// 止まるほうが害が大きい。値が読めなかった項目は単に現れない。
/// </para>
/// </remarks>
internal static class FrontMatterReader
{
    /// <summary>
    /// フロント マターの読み取り結果。
    /// </summary>
    /// <param name="Metadata">読み取られたメタデータ。</param>
    /// <param name="Diagnostics">読み取り中に報告された診断。</param>
    public readonly record struct Result(
        DocumentMetadata Metadata,
        ImmutableArray<Diagnostic> Diagnostics);

    /// <summary>
    /// YAML 本体を読み取る。
    /// </summary>
    /// <param name="yaml">YAML 本体。開始行と終了行は含まない。</param>
    /// <param name="path">診断に添えるコンテンツのパス。</param>
    /// <param name="firstYamlLine">YAML 本体の最初の行の、文書中での 1 起算の行番号。</param>
    /// <returns>読み取り結果。</returns>
    public static Result Read(
        string yaml,
        ContentPath path,
        int firstYamlLine)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        var root = LoadRoot(yaml, path, firstYamlLine, diagnostics);

        if (root is null)
        {
            return new Result(DocumentMetadata.Empty, diagnostics.DrainToImmutable());
        }

        if (root is not YamlMappingNode mapping)
        {
            // 最上位が並びやスカラーだと、キーと値の対応に写せない。
            // 中身を捨てる判断なので黙って行わない。
            diagnostics.Add(new Diagnostic(
                DiagnosticIds.FrontMatterNotMapping,
                DiagnosticSeverity.Warning,
                Messages.FormatFrontMatterNotMapping(root.NodeType),
                path,
                Locate(root, firstYamlLine)));

            return new Result(DocumentMetadata.Empty, diagnostics.DrainToImmutable());
        }

        var metadata = ReadMapping(mapping, path, firstYamlLine, diagnostics);

        return new Result(metadata, diagnostics.DrainToImmutable());
    }

    /// <summary>
    /// YAML を読み込み、最上位のノードを得る。
    /// </summary>
    /// <param name="yaml">YAML 本体。</param>
    /// <param name="path">診断に添えるコンテンツのパス。</param>
    /// <param name="firstYamlLine">YAML 本体の最初の行の行番号。</param>
    /// <param name="diagnostics">診断の収集先。</param>
    /// <returns>最上位のノード。文書が無い場合と解析できなかった場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// 空・空白のみ・コメントのみの YAML は文書が 0 個になる。これは誤りではないので、
    /// 診断を出さずに <see langword="null"/> を返す（メタデータが空になるだけである）。
    /// </remarks>
    private static YamlNode? LoadRoot(
        string yaml,
        ContentPath path,
        int firstYamlLine,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var stream = new YamlStream();

        try
        {
            stream.Load(new StringReader(yaml));
        }
        catch (YamlException ex)
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticIds.FrontMatterNotWellFormed,
                DiagnosticSeverity.Warning,
                Messages.FormatFrontMatterNotWellFormed(ex.Message),
                path,
                ToLocation(ex.Start, firstYamlLine)));

            return null;
        }

        return stream.Documents.Count == 0
            ? null
            : stream.Documents[0].RootNode;
    }

    /// <summary>
    /// 最上位のマッピングを <see cref="DocumentMetadata"/> に写す。
    /// </summary>
    /// <param name="mapping">最上位のマッピング。</param>
    /// <param name="path">診断に添えるコンテンツのパス。</param>
    /// <param name="firstYamlLine">YAML 本体の最初の行の行番号。</param>
    /// <param name="diagnostics">診断の収集先。</param>
    /// <returns>写されたメタデータ。</returns>
    /// <remarks>
    /// 出所は <see cref="MetadataProvenance.Declared"/> で、位置は<em>キー</em>の位置にする。
    /// 利用者が直すのはキーが書かれた行だからである。
    /// </remarks>
    private static DocumentMetadata ReadMapping(
        YamlMappingNode mapping,
        ContentPath path,
        int firstYamlLine,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var entries = ImmutableArray.CreateBuilder<MetadataEntry>(mapping.Children.Count);
        var rawKeys = new Dictionary<MetadataKey, string>();

        foreach (var child in mapping.Children)
        {
            if (child.Key is not YamlScalarNode scalarKey)
            {
                // YAML は並びやマッピングもキーにできる（`? [1,2]\n: v`）。
                // MetadataKey は文字列なので写せない。
                diagnostics.Add(new Diagnostic(
                    DiagnosticIds.FrontMatterKeyNotScalar,
                    DiagnosticSeverity.Warning,
                    Messages.FormatFrontMatterKeyNotScalar(child.Key.NodeType),
                    path,
                    Locate(child.Key, firstYamlLine)));

                continue;
            }

            var raw = scalarKey.Value;

            if (string.IsNullOrWhiteSpace(raw))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticIds.FrontMatterEmptyKey,
                    DiagnosticSeverity.Warning,
                    Messages.FrontMatterEmptyKey,
                    path,
                    Locate(scalarKey, firstYamlLine)));

                continue;
            }

            var key = MetadataKey.Create(raw);
            var location = Locate(scalarKey, firstYamlLine);

            // YamlDotNet は完全に同じキーの重複を例外にするが、正規化で衝突する
            // `page_title` と `page-title` は通る。後のものが勝つが、黙って落ちると
            // 「書いたのに効かない」になるので記録する。
            if (rawKeys.TryGetValue(key, out var previousRaw))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticIds.FrontMatterDuplicateKey,
                    DiagnosticSeverity.Warning,
                    Messages.FormatFrontMatterDuplicateKey(previousRaw, raw, key.Value),
                    path,
                    location));
            }

            rawKeys[key] = raw;

            var value = Convert(child.Value, key, path, location, diagnostics);

            entries.Add(new MetadataEntry(key, value, MetadataProvenance.Declared(location)));
        }

        return DocumentMetadata.Create(entries.DrainToImmutable());
    }

    /// <summary>
    /// YAML のノードを <see cref="MetadataValue"/> に写す。
    /// </summary>
    /// <param name="node">写すノード。</param>
    /// <param name="key">この値が属するキー。診断に用いる。</param>
    /// <param name="path">診断に添えるコンテンツのパス。</param>
    /// <param name="location">診断に添える位置。</param>
    /// <param name="diagnostics">診断の収集先。</param>
    /// <returns>写された値。</returns>
    /// <remarks>
    /// <para>
    /// スカラーは文字列のまま持つ。<c>draft: true</c> を
    /// <see cref="MetadataValue.Flag"/> にしないのは、YAML の <c>true</c> が
    /// 真偽値として書かれたのか文字列として書かれたのかを区別せず潰すと、
    /// <c>version: 1.0</c> のような値まで型を推測することになるためである。
    /// 真偽としての読み取りは <see cref="MetadataValue.TryGetBoolean"/> が担う。
    /// </para>
    /// <para>
    /// アンカーによる循環を検出する。YAML は <c>a: &amp;x [*x]</c> と書けて、
    /// YamlDotNet はこれを自分自身を要素に持つノードとして返す。
    /// そのまま再帰すると無限に降りるので、通ってきたノードを覚えて打ち切る。
    /// </para>
    /// </remarks>
    private static MetadataValue Convert(
        YamlNode node,
        MetadataKey key,
        ContentPath path,
        SourceLocation location,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var visiting = new HashSet<YamlNode>(ReferenceEqualityComparer.Instance);

        return ConvertCore(node, key, path, location, visiting, diagnostics);
    }

    /// <summary>
    /// <see cref="Convert"/> の本体。
    /// </summary>
    /// <param name="node">写すノード。</param>
    /// <param name="key">この値が属するキー。</param>
    /// <param name="path">診断に添えるコンテンツのパス。</param>
    /// <param name="location">診断に添える位置。</param>
    /// <param name="visiting">現在辿っている経路上のノード。</param>
    /// <param name="diagnostics">診断の収集先。</param>
    /// <returns>写された値。</returns>
    private static MetadataValue ConvertCore(
        YamlNode node,
        MetadataKey key,
        ContentPath path,
        SourceLocation location,
        HashSet<YamlNode> visiting,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        switch (node)
        {
            case YamlScalarNode scalar:
                return new MetadataValue.Scalar(scalar.Value ?? string.Empty);

            case YamlSequenceNode sequence:
            {
                if (!visiting.Add(node))
                {
                    return ReportCycle(key, path, location, diagnostics, new MetadataValue.Sequence([]));
                }

                var items = ImmutableArray.CreateBuilder<MetadataValue>(sequence.Children.Count);

                foreach (var item in sequence.Children)
                {
                    items.Add(ConvertCore(item, key, path, location, visiting, diagnostics));
                }

                visiting.Remove(node);

                return new MetadataValue.Sequence(items.DrainToImmutable());
            }

            case YamlMappingNode mapping:
            {
                if (!visiting.Add(node))
                {
                    return ReportCycle(
                        key,
                        path,
                        location,
                        diagnostics,
                        new MetadataValue.Mapping([]));
                }

                var entries = ImmutableDictionary.CreateBuilder<MetadataKey, MetadataValue>();

                foreach (var child in mapping.Children)
                {
                    // 入れ子のキーが非スカラーの場合は診断を出さずに落とす。
                    // 最上位と違い、入れ子の構造は消費者が解釈するものであり、
                    // 全階層について警告を出すと壊れた 1 ファイルが大量の診断を生む。
                    if (child.Key is YamlScalarNode { Value: { Length: > 0 } childKey })
                    {
                        entries[MetadataKey.Create(childKey)] =
                            ConvertCore(child.Value, key, path, location, visiting, diagnostics);
                    }
                }

                visiting.Remove(node);

                return new MetadataValue.Mapping(entries.ToImmutable());
            }

            default:
                // YamlAliasNode は Load 済みのモデルには現れない（解決済みのノードに置き換わる）。
                // それ以外の種類も無いが、網羅性のために空のスカラーにする。
                return new MetadataValue.Scalar(string.Empty);
        }
    }

    /// <summary>
    /// 循環を診断として報告し、代わりの値を返す。
    /// </summary>
    /// <param name="key">循環を含む値のキー。</param>
    /// <param name="path">診断に添えるコンテンツのパス。</param>
    /// <param name="location">診断に添える位置。</param>
    /// <param name="diagnostics">診断の収集先。</param>
    /// <param name="replacement">循環の位置に置く値。</param>
    /// <returns><paramref name="replacement"/>。</returns>
    private static MetadataValue ReportCycle(
        MetadataKey key,
        ContentPath path,
        SourceLocation location,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        MetadataValue replacement)
    {
        diagnostics.Add(new Diagnostic(
            DiagnosticIds.FrontMatterRecursiveAlias,
            DiagnosticSeverity.Warning,
            Messages.FormatFrontMatterRecursiveAlias(key.Value),
            path,
            location));

        return replacement;
    }

    /// <summary>
    /// ノードの位置を文書中の位置に直す。
    /// </summary>
    /// <param name="node">対象のノード。</param>
    /// <param name="firstYamlLine">YAML 本体の最初の行の行番号。</param>
    /// <returns>文書中の位置。</returns>
    private static SourceLocation Locate(
        YamlNode node,
        int firstYamlLine)
    {
        return ToLocation(node.Start, firstYamlLine);
    }

    /// <summary>
    /// YAML の位置を文書中の位置に直す。
    /// </summary>
    /// <param name="mark">YAML 本体の中での位置。</param>
    /// <param name="firstYamlLine">YAML 本体の最初の行の行番号。</param>
    /// <returns>文書中の位置。</returns>
    /// <remarks>
    /// YamlDotNet の <see cref="Mark.Line"/> は 1 起算で、YAML 本体の中での行番号である。
    /// 文書中の行番号にするには本体の開始行を足して 1 を引く。
    /// 桁はそのまま使える（フロント マターは字下げされないため）。
    /// </remarks>
    private static SourceLocation ToLocation(
        Mark mark,
        int firstYamlLine)
    {
        if (mark.Line <= 0)
        {
            return default;
        }

        return new SourceLocation(
            checked((int)mark.Line + firstYamlLine - 1),
            checked((int)mark.Column));
    }
}
