using System.Collections.Generic;
using System.Collections.Immutable;

using Lithify.Abstractions;
using Lithify.Core.Incremental;
using Lithify.Core.Output;

namespace Lithify.Core.Building;

/// <summary>
/// 1 つの出力に対して起きたこと。
/// </summary>
/// <param name="Path">出力のパス。</param>
/// <param name="Outcome">書き込みの結果。</param>
public readonly record struct OutputWrite(
    OutputPath Path,
    WriteOutcome Outcome);

/// <summary>
/// ビルド 1 回の結果。
/// </summary>
/// <param name="Revision">このビルドが対象としたリビジョン。</param>
/// <param name="Diagnostics">報告された診断。</param>
/// <param name="Writes">対象となった出力とその結果。</param>
/// <remarks>
/// <para>
/// <paramref name="Writes"/> に <see cref="WriteOutcome.Unchanged"/> の項目も含めるのは、
/// 「評価はされたが書かれなかった」ことが増分ビルドの成否そのものだからである。
/// これを落とすと R7 が働いているかどうかを外から観測できない。
/// </para>
/// <para>
/// live-reload は <see cref="ChangedPaths"/> をそのまま購読中のブラウザーに通知する。
/// 変更ページを検出する別の仕組みは持たない（R9）。
/// </para>
/// </remarks>
public sealed record BuildReport(
    Revision Revision,
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<OutputWrite> Writes)
{
    /// <summary>
    /// 空の結果を取得する。
    /// </summary>
    public static BuildReport Empty { get; } =
        new(Revision.None, [], []);

    /// <summary>
    /// エラーの診断が 1 件以上あるかどうかを示す値を取得する。
    /// </summary>
    /// <remarks>
    /// CLI の終了コードはこの値から決まる。
    /// </remarks>
    public bool HasErrors
    {
        get
        {
            foreach (var diagnostic in this.Diagnostics)
            {
                if (diagnostic.IsError)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 実際に内容が変わった出力のパスを列挙する。
    /// </summary>
    /// <returns>内容が変わった出力のパス。</returns>
    public IEnumerable<OutputPath> ChangedPaths()
    {
        foreach (var write in this.Writes)
        {
            if (write.Outcome != WriteOutcome.Unchanged)
            {
                yield return write.Path;
            }
        }
    }
}
