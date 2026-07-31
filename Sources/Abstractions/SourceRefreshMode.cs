namespace Lithify.Abstractions;

/// <summary>
/// 取得済みのコンテンツを取り直すかどうかについての利用者の指示。
/// </summary>
/// <remarks>
/// <para>
/// 鮮度の判定そのものはプロバイダの私事だが（<see cref="ContentSourceResult.Unchanged"/> 参照）、
/// <strong>通信を許すかどうかは利用者の判断である。</strong> ゆえにこれだけを抽象に出す。
/// </para>
/// <para>
/// <strong><c>LithifyOptions.Force</c> とは別の軸なので統合してはならない。</strong>
/// <c>Force</c> は出力の書き直し、これは入力の再取得である。
/// 「キャッシュを信じないが網には出たくない」は正当な組み合わせとして要る。
/// </para>
/// </remarks>
public enum SourceRefreshMode
{
    /// <summary>
    /// 鮮度が切れていれば再確認する。
    /// </summary>
    /// <remarks>
    /// 既定値。通常のビルド。
    /// </remarks>
    Default,

    /// <summary>
    /// 鮮度を無視して必ず再確認する。
    /// </summary>
    /// <remarks>
    /// 公開前の最終ビルド。<c>--refresh-sources</c> で指定する。
    /// </remarks>
    Always,

    /// <summary>
    /// 通信しない。鮮度が切れていてもストアの内容を使う。
    /// </summary>
    /// <remarks>
    /// <para>
    /// オフラインでのビルド。<c>--offline</c> で指定する。
    /// </para>
    /// <para>
    /// 内容がストアに無い場合は <see cref="ContentSourceResult.Unavailable"/> である
    /// （<see cref="ContentSourceResult.Missing"/> ではない。存在しないことを確認できていない）。
    /// </para>
    /// <para>
    /// <strong>これは <see cref="ReproducibilityMode.Require"/> の代用にならない。</strong>
    /// オフラインでもストアの内容は取得時点のものであり、
    /// それが一意なアドレス由来かどうかは別の問題である。
    /// </para>
    /// </remarks>
    Never,
}
