using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;

namespace Lithify.Abstractions;

/// <summary>
/// テンプレート ソースを再利用可能なレンダリング単位に変換する。
/// </summary>
/// <remarks>
/// <para>
/// コンパイルとレンダリングの 2 段に分けているのは、対象エンジンが実際にその構造を持つからである。
/// Handlebars.Net は <c>Handlebars.Compile()</c> が <c>HandlebarsTemplate&lt;,&gt;</c> デリゲートを返し、
/// Fluid は <c>FluidParser.TryParse()</c> が <c>IFluidTemplate</c> を返す。
/// いずれも構文解析結果を保持して何度もレンダリングできる。
/// </para>
/// <para>
/// Blazor はこの抽象の妥当性を試す例外である。Blazor コンポーネントは Razor コンパイラが
/// <em>ビルド時</em>に IL へ変換するので、実行時の「コンパイル」に相当するものがない。
/// <c>Lithify.Templates.Blazor</c> の <see cref="CompileAsync"/> は
/// <see cref="TemplateSource"/> を型名として解決し、
/// <see cref="ICompiledTemplate.Fingerprint"/> はアセンブリの MVID から作る。
/// つまり Blazor では「テンプレートを直したら再ビルドが必要」になる。
/// これは Blazor を選ぶことの本質的な帰結であり、抽象を歪めて隠すべきものではない。
/// </para>
/// </remarks>
public interface ITemplateEngine
{
    /// <summary>
    /// このエンジンの名前を取得する。
    /// </summary>
    /// <remarks>
    /// <c>handlebars</c> / <c>liquid</c> / <c>blazor</c>。
    /// 診断メッセージと構成で参照される。
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// テンプレートをコンパイルする。
    /// </summary>
    /// <param name="source">テンプレート ソース。</param>
    /// <param name="cancellationToken">取り消しトークン。</param>
    /// <returns>コンパイル済みテンプレート。</returns>
    /// <remarks>
    /// 構文エラーはここで報告される。1000 ページ目で初めて構文エラーが判るのを避けるため、
    /// コンパイルをレンダリングより前に置いている。
    /// </remarks>
    ValueTask<ICompiledTemplate> CompileAsync(
        TemplateSource source,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 解析済みで、任意のモデルに対し何度でも実行できるテンプレート。
/// </summary>
/// <remarks>
/// これ自体が計算ノードの値になるので、テンプレートが変わらない限りキャッシュされる。
/// 1000 記事のサイトで <c>post.hbs</c> を 1000 回パースすることはない。
/// </remarks>
public interface ICompiledTemplate : IFingerprintable
{
    /// <summary>
    /// このテンプレート自身と、それが依存する partial 群を合わせたフィンガープリントを取得する。
    /// </summary>
    /// <remarks>
    /// partial（<c>_sidebar.hbs</c> 等）を含めた<em>合成</em>フィンガープリントであることが要点。
    /// テンプレート自身のみのハッシュにすると partial の変更が伝播せず、
    /// partial を直しても再レンダリングが起きない。
    /// </remarks>
    new Fingerprint Fingerprint { get; }

    /// <summary>
    /// モデルを適用してレンダリングする。
    /// </summary>
    /// <param name="model">テンプレートに渡すモデル。</param>
    /// <param name="writer">出力先。</param>
    /// <param name="cancellationToken">取り消しトークン。</param>
    /// <returns>レンダリングの完了を表すタスク。</returns>
    ValueTask RenderAsync(
        ITemplateModel model,
        TextWriter writer,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// テンプレートに渡されるモデル。
/// </summary>
/// <remarks>
/// <para>
/// POCO ではなく辞書ライクな読み取り専用ビューにしている。Handlebars.Net と Fluid はどちらも
/// POCO をリフレクションで束縛できるが、それに乗ると「テンプレートが触ったプロパティ」が判らず、
/// モデルの一部が変わっただけで全ページが無効化される。
/// </para>
/// <para>
/// 明示的なアクセス経路にしておけば、将来テンプレートの実際の参照だけを
/// 依存として記録する余地が残る（骨格段階では実装しない）。
/// </para>
/// </remarks>
public interface ITemplateModel
{
    /// <summary>
    /// このモデルが持つ名前を取得する。
    /// </summary>
    IReadOnlyCollection<string> Names { get; }

    /// <summary>
    /// 名前に対応する値を取得する。
    /// </summary>
    /// <param name="name">値の名前。</param>
    /// <param name="value">対応する値。</param>
    /// <returns>対応する値が存在する場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// 値は <see cref="string"/>、<see cref="bool"/>、数値、
    /// <see cref="ITemplateModel"/>、<see cref="IReadOnlyList{T}"/> のいずれかである。
    /// </remarks>
    bool TryGetValue(
        string name,
        [MaybeNullWhen(false)] out object value);
}

/// <summary>
/// テンプレートの元となる記述。
/// </summary>
/// <remarks>
/// <see cref="Text"/> と <see cref="TypeName"/> のどちらか一方だけが値を持つ。
/// 前者はテキストからコンパイルするエンジン（Handlebars / Liquid）、
/// 後者は型を参照するエンジン（Blazor）に対応する。
/// </remarks>
public sealed record TemplateSource
{
    private TemplateSource()
    {
    }

    /// <summary>
    /// このテンプレートの名前を取得する。
    /// </summary>
    /// <remarks>
    /// レイアウト名（<c>post</c> / <c>layout</c>）として参照される識別子。
    /// 診断メッセージにも用いられる。
    /// </remarks>
    public string Name { get; private init; } = string.Empty;

    /// <summary>
    /// テンプレートの本文を取得する。テキストを持たない場合は <see langword="null"/>。
    /// </summary>
    public string? Text { get; private init; }

    /// <summary>
    /// テンプレートに対応する型の名前を取得する。型で表されない場合は <see langword="null"/>。
    /// </summary>
    public string? TypeName { get; private init; }

    /// <summary>
    /// このテンプレート ソースの由来を取得する。特定できない場合は <see langword="default"/>。
    /// </summary>
    /// <remarks>
    /// 診断メッセージでファイル位置を示すために保持する。
    /// </remarks>
    public ContentPath Path { get; private init; }

    /// <summary>
    /// テキストからテンプレート ソースを生成する。
    /// </summary>
    /// <param name="name">テンプレートの名前。</param>
    /// <param name="text">テンプレートの本文。</param>
    /// <param name="path">テンプレートの由来。</param>
    /// <returns>生成された <see cref="TemplateSource"/>。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> または <paramref name="text"/> が <see langword="null"/> である。
    /// </exception>
    [Pure]
    public static TemplateSource FromText(
        string name,
        string text,
        ContentPath path = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(text);

        return new TemplateSource
        {
            Name = name,
            Text = text,
            Path = path,
        };
    }

    /// <summary>
    /// 型名からテンプレート ソースを生成する。
    /// </summary>
    /// <param name="name">テンプレートの名前。</param>
    /// <param name="typeName">テンプレートに対応する型の名前。</param>
    /// <returns>生成された <see cref="TemplateSource"/>。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> または <paramref name="typeName"/> が <see langword="null"/> である。
    /// </exception>
    /// <remarks>
    /// Blazor コンポーネントのように、テンプレートがビルド時に IL へ変換されている場合に用いる。
    /// </remarks>
    [Pure]
    public static TemplateSource FromTypeName(
        string name,
        string typeName)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(typeName);

        return new TemplateSource
        {
            Name = name,
            TypeName = typeName,
        };
    }
}
