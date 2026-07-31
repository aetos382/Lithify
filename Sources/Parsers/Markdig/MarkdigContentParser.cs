using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Lithify.Abstractions;
using Lithify.Abstractions.Ast;
using Lithify.Markdown.Abstractions;

using Markdig;

using Microsoft.Extensions.Options;

namespace Lithify.Parsers.Markdig;

/// <summary>
/// Markdig による Markdown のパーサー。
/// </summary>
/// <remarks>
/// <para>
/// <strong>2 つの経路を持ち、どちらも同じメタデータを返さなければならない。</strong>
/// <see cref="ParseMetadataAsync"/> は <see cref="FrontMatterScanner"/> と
/// <see cref="FrontMatterReader"/> だけを通り、Markdig のパイプラインを組み立てない。
/// <see cref="ParseAsync"/> は同じ 2 つに加えて Markdig を通す。
/// メタデータを得る部分を共有している（<see cref="ReadMetadata"/>）のは、
/// 経路ごとに書くと契約テストが検証する一致がコードの重複に依存するためである。
/// </para>
/// <para>
/// <see cref="MarkdownPipeline"/> を毎回組み立てないのは、拡張の登録と
/// パーサーの構築が入力の長さに依存しない固定費であり、
/// 設定が <see cref="IOptions{TOptions}"/> 経由で固定されているためである。
/// <see cref="MarkdownPipeline"/> 自体は状態を持たず、並行に使える。
/// </para>
/// <para>
/// <see cref="IOptions{TOptions}"/> ではなく <see cref="IOptionsMonitor{TOptions}"/> を
/// 使わないのは、設定が 1 回のビルドの間に変わると、同じサイトの中でページごとに
/// 別の方言で解釈されうるためである。設定の変更はプロセスの再起動で反映する。
/// </para>
/// </remarks>
internal sealed class MarkdigContentParser :
    IContentParser
{
    private readonly MarkdownPipeline _pipeline;

    /// <summary>
    /// <see cref="MarkdigContentParser"/> を生成する。
    /// </summary>
    /// <param name="options">Markdown の形式の設定。</param>
    /// <param name="engineOptions">Markdig 固有の設定。</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> または <paramref name="engineOptions"/> が <see langword="null"/> である。
    /// </exception>
    public MarkdigContentParser(
        IOptions<MarkdownOptions> options,
        IOptions<MarkdigOptions> engineOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(engineOptions);

        this._pipeline = MarkdownPipelineFactory.Create(options.Value, engineOptions.Value);
    }

    /// <inheritdoc />
    public ImmutableArray<ContentFormat> SupportedFormats { get; } =
        [ContentFormat.Markdown];

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="source"/> が <see langword="null"/> である。</exception>
    /// <remarks>
    /// <para>
    /// 診断は<em>返さない</em>。<see cref="IContentParser.ParseMetadataAsync"/> の戻り値に
    /// 診断の場所が無いためである。壊れたフロント マターは
    /// <see cref="ParseAsync"/> が同じ入力に対して同じ診断を出すので、
    /// 落とすことによって報告されない誤りは生じない
    /// （軽量パスだけを通ってページが出力されることはない）。
    /// </para>
    /// </remarks>
    public async ValueTask<DocumentMetadata> ParseMetadataAsync(
        ContentSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var text = await source.ReadAsync(cancellationToken).ConfigureAwait(false);

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        return ReadMetadata(text, source, diagnostics);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="source"/> が <see langword="null"/> である。</exception>
    public async ValueTask<ParseResult> ParseAsync(
        ContentSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var text = await source.ReadAsync(cancellationToken).ConfigureAwait(false);

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        // メタデータは軽量パスと同じ経路で読む。Markdig が持つ
        // YamlFrontMatterBlock から読み直すと、2 つの経路が別の実装になる。
        var metadata = ReadMetadata(text, source, diagnostics);

        cancellationToken.ThrowIfCancellationRequested();

        // global:: が要る。この名前空間の中では Markdown が Lithify.Markdown に、
        // Markdig が Lithify.Parsers.Markdig に束縛されるので、
        // Markdig.Markdown も Markdown も Markdig の型を指さない。
        var document = global::Markdig.Markdown.Parse(text, this._pipeline);

        var context = new MarkdigMappingContext(source.Path, diagnostics);

        var mapped = MarkdigBlockMapper.MapDocument(document, metadata, context);

        return new ParseResult(mapped, diagnostics.DrainToImmutable());
    }

    /// <summary>
    /// 文書先頭のフロント マターからメタデータを読む。
    /// </summary>
    /// <param name="text">文書全体。</param>
    /// <param name="source">対象のコンテンツ。</param>
    /// <param name="diagnostics">診断の収集先。</param>
    /// <returns>読み取られたメタデータ。</returns>
    /// <remarks>
    /// <para>
    /// フロント マターが無い場合も <see cref="WellKnownMetadataMapper"/> は通す。
    /// <see cref="WellKnownMetadata.SourceFormat"/> は内容に依存しないので、
    /// フロント マターを書いていない文書でも記録されなければならない。
    /// </para>
    /// <para>
    /// <see cref="FrontMatterScanner.FrontMatter"/> は <see langword="ref struct"/> なので、
    /// この関数は <see langword="async"/> にできない。読み取りは呼び出し側で済ませておく。
    /// </para>
    /// </remarks>
    private static DocumentMetadata ReadMetadata(
        string text,
        ContentSource source,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var metadata = DocumentMetadata.Empty;

        if (FrontMatterScanner.TryScan(text, out var frontMatter))
        {
            var result = FrontMatterReader.Read(
                frontMatter.Yaml.ToString(),
                source.Path,
                frontMatter.FirstYamlLine);

            metadata = result.Metadata;

            diagnostics.AddRange(result.Diagnostics);
        }

        // 形式は source.Format ではなくこのパーサーが扱う形式にする。
        // 呼び出し側が別の形式を持つ ContentSource を渡しても、実際に解釈したのは Markdown である。
        return WellKnownMetadataMapper.Map(metadata, ContentFormat.Markdown, source.Path, diagnostics);
    }
}
