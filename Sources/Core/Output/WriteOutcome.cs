using JetBrains.Annotations;

using Lithify.Abstractions;

namespace Lithify.Core.Output;

/// <summary>
/// 出力の書き込みで起きたこと。
/// </summary>
public enum WriteOutcome
{
    /// <summary>
    /// 新しく作成された。
    /// </summary>
    Created,

    /// <summary>
    /// 内容が変わったため上書きされた。
    /// </summary>
    Updated,

    /// <summary>
    /// 内容が同じだったため書き込まれなかった。更新日時は変わらない。
    /// </summary>
    Unchanged,
}

/// <summary>
/// 出力を書き込むべきかどうかの判断。
/// </summary>
/// <remarks>
/// <para>
/// 判断を I/O から分離して純粋関数として置くのは、R7（内容が本質的に変わらなければ更新日時を
/// 変えない）の要がこの判断そのものだからである。<c>WriteAsync(path, content)</c> ひとつに
/// 埋め込むと、最も重要なロジックを検証するのにファイルシステムを触る必要が生じる。
/// </para>
/// <para>
/// この判断は live-reload の変更検出も兼ねる。「実際に内容が変わった出力パス」は
/// <see cref="WriteOutcome"/> が <see cref="WriteOutcome.Unchanged"/> でないものの集合そのものなので、
/// 変更ページを検出する別の仕組みを持たない（R9）。
/// </para>
/// </remarks>
public static class OutputDecision
{
    /// <summary>
    /// 書き込むべきかどうかを判断する。
    /// </summary>
    /// <param name="desired">書き込もうとしている内容のフィンガープリント。</param>
    /// <param name="existing">既存の出力のフィンガープリント。存在しない場合は <see langword="null"/>。</param>
    /// <returns>判断の結果。</returns>
    /// <remarks>
    /// 「本質的に変わらない」の基準はバイト列の一致である。空白の差異を無視するといった正規化は
    /// レンダラーの責務であり、ここで行うと「レンダラーが変わったのに出力が更新されない」ことになる。
    /// </remarks>
    [Pure]
    public static WriteOutcome Decide(
        Fingerprint desired,
        Fingerprint? existing)
    {
        if (existing is not { } actual)
        {
            return WriteOutcome.Created;
        }

        return actual == desired
            ? WriteOutcome.Unchanged
            : WriteOutcome.Updated;
    }
}
