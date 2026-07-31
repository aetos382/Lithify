using System.Globalization;

namespace Lithify.Abstractions;

/// <summary>
/// 診断の重大度。
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>
    /// 情報。ビルドは継続する。
    /// </summary>
    /// <remarks>
    /// 「後から登録されたパーサーが先のものを上書きした」のように、
    /// 意図的な操作かもしれないが黙って行うべきでないことを記録するのに使う。
    /// </remarks>
    Information,

    /// <summary>
    /// 警告。ビルドは継続する。
    /// </summary>
    Warning,

    /// <summary>
    /// エラー。ビルドは中止される。
    /// </summary>
    Error,
}

/// <summary>
/// ソース中の位置。
/// </summary>
/// <param name="Line">1 から始まる行番号。</param>
/// <param name="Column">1 から始まる桁番号。</param>
/// <remarks>
/// パーサーが位置情報を提供しない場合は <see langword="default"/>（<c>0, 0</c>）を用いる。
/// 0 は「位置不明」を意味する。
/// </remarks>
public readonly record struct SourceLocation(
    int Line,
    int Column)
{
    /// <summary>
    /// この位置が不明であるかどうかを示す値を取得する。
    /// </summary>
    public bool IsUnknown =>
        this.Line <= 0;
}

/// <summary>
/// ビルド中に報告される診断。
/// </summary>
/// <param name="Id">診断の識別子（<c>LI0001</c> 等）。</param>
/// <param name="Severity">重大度。</param>
/// <param name="Message">利用者に提示する本文。</param>
/// <param name="Path">診断の対象となったコンテンツ。特定できない場合は <see langword="default"/>。</param>
/// <param name="Location">コンテンツ中の位置。特定できない場合は <see langword="default"/>。</param>
/// <remarks>
/// <para>
/// 例外ではなく値として返す。パースは 1 つの誤りで止めるより、
/// 見つかった誤りをすべて集めて提示したほうが有用である。
/// </para>
/// <para>
/// 診断は増分計算グラフのノードの値の一部として扱われる。したがって
/// キャッシュ ヒットしたページの診断も、再計算せずに再提示できる。
/// これがないと 2 回目のビルドで警告が消えてしまう。
/// </para>
/// </remarks>
public sealed record Diagnostic(
    string Id,
    DiagnosticSeverity Severity,
    string Message,
    ContentPath Path,
    SourceLocation Location)
{
    /// <summary>
    /// 位置情報を持たない診断を生成する。
    /// </summary>
    /// <param name="id">診断の識別子。</param>
    /// <param name="severity">重大度。</param>
    /// <param name="message">利用者に提示する本文。</param>
    /// <param name="path">診断の対象となったコンテンツ。</param>
    public Diagnostic(
        string id,
        DiagnosticSeverity severity,
        string message,
        ContentPath path)
        : this(id, severity, message, path, default)
    {
    }

    /// <summary>
    /// 対象のコンテンツを持たない診断を生成する。
    /// </summary>
    /// <param name="id">診断の識別子。</param>
    /// <param name="severity">重大度。</param>
    /// <param name="message">利用者に提示する本文。</param>
    /// <remarks>
    /// 構成の誤り（同じ形式を扱うパーサーが複数登録された等）のように、
    /// <em>コンテンツを 1 つも読まずに決まる</em>診断に用いる。
    /// パスが分かるのに省略するためのものではない。
    /// 対象を特定できるなら常に渡すべきで、渡さなければ利用者は
    /// どのファイルの話かを知る手立てがない。
    /// </remarks>
    public Diagnostic(
        string id,
        DiagnosticSeverity severity,
        string message)
        : this(id, severity, message, default, default)
    {
    }

    /// <summary>
    /// この診断がビルドを中止させるかどうかを示す値を取得する。
    /// </summary>
    public bool IsError =>
        this.Severity == DiagnosticSeverity.Error;

    /// <summary>
    /// 診断を <c>path(line,column): severity id: message</c> 形式で表す。
    /// </summary>
    /// <returns>診断の文字列表現。</returns>
    /// <remarks>
    /// MSBuild の診断形式に合わせている。CLI の出力をエディターや CI が解釈できるようにするため。
    /// </remarks>
    public override string ToString()
    {
        var severity = this.Severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            _ => "info",
        };

        var origin = this.Path.IsEmpty
            ? string.Empty
            : this.Location.IsUnknown
                ? string.Concat(this.Path.Value, ": ")
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}({1},{2}): ",
                    this.Path.Value,
                    this.Location.Line,
                    this.Location.Column);

        return string.Concat(origin, severity, " ", this.Id, ": ", this.Message);
    }
}
