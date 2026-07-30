namespace Lithify.Hosting;

/// <summary>
/// CLI の終了コード。
/// </summary>
/// <remarks>
/// 例外を投げるのではなく終了コードで表すのは、CLI の失敗が「プログラムの誤り」ではなく
/// 「入力の誤り」だからである。文書の誤りは <c>Diagnostic</c> として集めて提示し、
/// 最初の 1 件でビルドを止めない。
/// </remarks>
internal static class ExitCodes
{
    /// <summary>
    /// 成功。
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// コマンドラインの解析に失敗した。
    /// </summary>
    public const int UsageError = 1;

    /// <summary>
    /// エラーの診断が報告された。
    /// </summary>
    public const int DiagnosticError = 2;
}
