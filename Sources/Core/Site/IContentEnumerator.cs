using System.Collections.Generic;
using System.Threading;

using Lithify.Abstractions;

namespace Lithify.Core.Site;

/// <summary>
/// サイトの入力を列挙する。
/// </summary>
/// <remarks>
/// <para>
/// 計算グラフの葉である。ここから得られたパスとフィンガープリントが、
/// すべての再計算の起点になる。
/// </para>
/// <para>
/// <see cref="ContentSource"/> は内容を遅延読み取りするため、列挙の段階では
/// ファイルの中身を読まない。メタデータだけを必要とする経路（サイドバーのタグ一覧の構築）が
/// 全記事の本文を読まずに済むようにするためである。
/// </para>
/// </remarks>
public interface IContentEnumerator
{
    /// <summary>
    /// 指定したグロブ パターンに一致する入力を列挙する。
    /// </summary>
    /// <param name="patterns">グロブ パターン（<c>posts/**/*.md</c> 等）。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>入力の列挙。パスの辞書順に並ぶ。</returns>
    /// <remarks>
    /// 並び順を安定させるのは、ファイルシステムの列挙順に依存すると
    /// 同じ入力でもビルドの結果（診断の順序、フィンガープリントの合成順）が
    /// 実行環境によって変わってしまうためである。
    /// </remarks>
    IAsyncEnumerable<ContentSource> EnumerateAsync(
        IEnumerable<string> patterns,
        CancellationToken cancellationToken = default);
}
