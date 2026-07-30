using System.Threading;
using System.Threading.Tasks;

namespace Lithify.Core.Building;

/// <summary>
/// 生成物とビルド キャッシュを削除する。
/// </summary>
/// <remarks>
/// ビルドと別の型にしているのは、削除がグラフの外側の操作だからである。
/// <see cref="ISiteBuilder"/> に <c>CleanAsync</c> を足すと、需要駆動の評価器が
/// 「何も要求されていないのに副作用を起こす」経路を持つことになる。
/// </remarks>
public interface ISiteCleaner
{
    /// <summary>
    /// 生成物とビルド キャッシュを削除する。
    /// </summary>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>削除された出力の数。</returns>
    /// <remarks>
    /// 出力ルート配下で、ビルド キャッシュが記録している出力のみを削除する。
    /// ディレクトリごと消さないのは、利用者が出力ルートを他の生成物と共有している場合に
    /// 巻き込むのを避けるため。
    /// </remarks>
    ValueTask<int> CleanAsync(
        CancellationToken cancellationToken = default);
}
