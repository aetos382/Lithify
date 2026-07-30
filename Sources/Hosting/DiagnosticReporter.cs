using System;
using System.Collections.Immutable;
using System.Globalization;

using Lithify.Abstractions;

using Microsoft.Extensions.Logging;

namespace Lithify.Hosting;

/// <summary>
/// 診断を <see cref="ILogger"/> に流す。
/// </summary>
/// <remarks>
/// <see cref="Diagnostic.ToString"/> が MSBuild の診断形式を作るので、そのまま出せば
/// エディターや CI が位置情報を解釈できる。ここで整形し直すとその性質が失われる。
/// </remarks>
internal static class DiagnosticReporter
{
    // 重大度ごとに別のデリゲートを用意する。LoggerMessage.Define はレベルを
    // デリゲートに焼き込むので、実行時に決まるレベルを渡すことはできない。
    private static readonly Action<ILogger, Diagnostic, Exception?> LogError =
        LoggerMessage.Define<Diagnostic>(
            LogLevel.Error,
            new EventId(1, nameof(LogError)),
            "{Diagnostic}");

    private static readonly Action<ILogger, Diagnostic, Exception?> LogWarning =
        LoggerMessage.Define<Diagnostic>(
            LogLevel.Warning,
            new EventId(2, nameof(LogWarning)),
            "{Diagnostic}");

    private static readonly Action<ILogger, Diagnostic, Exception?> LogInformation =
        LoggerMessage.Define<Diagnostic>(
            LogLevel.Information,
            new EventId(3, nameof(LogInformation)),
            "{Diagnostic}");

    /// <summary>
    /// 診断をログに出力する。
    /// </summary>
    /// <param name="logger">出力先。</param>
    /// <param name="diagnostics">診断。</param>
    public static void Report(
        ILogger logger,
        ImmutableArray<Diagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            var log = diagnostic.Severity switch
            {
                DiagnosticSeverity.Error => LogError,
                DiagnosticSeverity.Warning => LogWarning,
                _ => LogInformation,
            };

            // Diagnostic をそのまま渡す。文字列化はフォーマッターが必要になった時点で
            // 起きるので、レベルが無効なら ToString() は呼ばれない。
            log(logger, diagnostic, null);
        }
    }

    /// <summary>
    /// 診断の件数を要約した文字列を返す。
    /// </summary>
    /// <param name="diagnostics">診断。</param>
    /// <returns>要約。</returns>
    public static string Summarize(
        ImmutableArray<Diagnostic> diagnostics)
    {
        var errors = 0;
        var warnings = 0;

        foreach (var diagnostic in diagnostics)
        {
            switch (diagnostic.Severity)
            {
                case DiagnosticSeverity.Error:
                    ++errors;
                    break;

                case DiagnosticSeverity.Warning:
                    ++warnings;
                    break;

                case DiagnosticSeverity.Information:
                default:
                    break;
            }
        }

        return string.Format(
            CultureInfo.CurrentCulture,
            "{0} error(s), {1} warning(s)",
            errors,
            warnings);
    }
}
