namespace Lithify.Abstractions;

/// <summary>
/// 内容の同一性を表すフィンガープリントを持つ値。
/// </summary>
/// <remarks>
/// 増分ビルドの early cutoff は、再計算した値のフィンガープリントが前回と一致するかどうかで判断される。
/// 実装は「フィンガープリントが等しいならば下流にとって同じ値である」という性質を満たさなければならない。
/// </remarks>
public interface IFingerprintable
{
    /// <summary>
    /// この値の内容から決まるフィンガープリントを取得する。
    /// </summary>
    Fingerprint Fingerprint { get; }
}
