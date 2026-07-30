using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Lithify.Abstractions;

/// <summary>
/// コード ブロックにシンタックス ハイライトを施す。
/// </summary>
/// <remarks>
/// <para>
/// 形式固有ではない。Markdown の fenced code block も AsciiDoc の source block も
/// 同じ <see cref="Ast.CodeBlockNode"/> になるため、この抽象は形式の区別を持たない。
/// </para>
/// <para>
/// ハイライトは <c>(code, language, </c><see cref="IFingerprintable.Fingerprint"/><c>)</c>
/// だけで結果が決まる純粋関数でありながら重い処理なので、独立した計算ノードにするとメモ化が効き、
/// <em>ビルドを跨いで、さらに同じスニペットを載せる複数ページ間で</em>共有される。
/// </para>
/// <para>
/// <see cref="IFingerprintable"/> を継承するのは、テーマや文法ファイルの変更を
/// 下流に正しく伝えるためである。実装は文法ファイル自身の内容をフィンガープリントに含めなければ、
/// 文法を更新しても再ハイライトが起きない。
/// </para>
/// </remarks>
public interface ISyntaxHighlighter : IFingerprintable
{
    /// <summary>
    /// 指定した言語を扱えるかどうかを判定する。
    /// </summary>
    /// <param name="language">言語識別子。</param>
    /// <returns>扱える場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// 偽を返した場合、呼び出し側は <see cref="PassThroughSyntaxHighlighter"/> に退避する。
    /// 未対応言語で例外を投げるのではなく退避させるのは、デグレードが安全な側に倒れるようにするためである。
    /// </remarks>
    bool CanHighlight(
        string language);

    /// <summary>
    /// コードにハイライトを施す。
    /// </summary>
    /// <param name="code">対象のコード。</param>
    /// <param name="language">言語識別子。</param>
    /// <param name="cancellationToken">取り消しトークン。</param>
    /// <returns>ハイライト結果。</returns>
    ValueTask<HighlightedCode> HighlightAsync(
        string code,
        string language,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// ハイライトされたコード。
/// </summary>
/// <param name="Spans">
/// トークンの並び。<see cref="CodeSpan.TextRange"/> は元のコード文字列に対する範囲を指す。
/// </param>
/// <remarks>
/// HTML 文字列ではなくトークン列を返す。HTML への変換をレンダラーの責務にしておくことで、
/// 将来 HTML 以外の出力形式を追加してもハイライターを作り直さずに済む。
/// </remarks>
public sealed record HighlightedCode(
    ImmutableArray<CodeSpan> Spans)
{
    /// <summary>
    /// トークンを持たないハイライト結果。
    /// </summary>
    public static HighlightedCode Empty { get; } = new([]);
}

/// <summary>
/// ハイライトされた 1 つのトークン。
/// </summary>
/// <param name="TextRange">元のコード文字列に対する範囲。</param>
/// <param name="Scope">このトークンに割り当てられたスコープ。</param>
public readonly record struct CodeSpan(
    Range TextRange,
    ScopeName Scope);

/// <summary>
/// ハイライトを行わない <see cref="ISyntaxHighlighter"/>。
/// </summary>
/// <remarks>
/// ハイライターが構成されていない場合や、対象言語が未対応の場合の退避先。
/// トークンを 1 つも返さないので、レンダラーはコードをそのまま
/// <c>&lt;code class="language-xxx"&gt;</c> に入れて出力する。
/// </remarks>
public sealed class PassThroughSyntaxHighlighter : ISyntaxHighlighter
{
    /// <summary>
    /// 共有インスタンス。
    /// </summary>
    /// <remarks>
    /// 状態を持たないので、インスタンスを作り分ける意味はない。
    /// </remarks>
    public static PassThroughSyntaxHighlighter Instance { get; } = new();

    private PassThroughSyntaxHighlighter()
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// 実装が変わらない限り一定でなければならないので、固定値を返す。
    /// </remarks>
    public Fingerprint Fingerprint { get; } =
        Fingerprint.OfString($"{nameof(PassThroughSyntaxHighlighter)}/1");

    /// <inheritdoc />
    /// <remarks>
    /// 常に <see langword="false"/> を返す。これによりレンダラーは
    /// <see cref="HighlightAsync"/> を呼ばずに済む。
    /// </remarks>
    public bool CanHighlight(
        string language)
    {
        return false;
    }

    /// <inheritdoc />
    public ValueTask<HighlightedCode> HighlightAsync(
        string code,
        string language,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(language);

        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(HighlightedCode.Empty);
    }
}
