using System;

namespace Lithify.Abstractions;

/// <summary>
/// <see cref="IContentSourceProvider.OpenAsync"/> の結果。
/// </summary>
/// <remarks>
/// <para>
/// <strong>4 分岐であることに意味がある。</strong> ローカル専用の設計では
/// <see cref="System.IO.FileNotFoundException"/> だけで足りていたが、
/// 取得が失敗しうる経路が増えると、区別しないことによる誤りが実害になる。
/// </para>
/// <para>
/// <strong>とくに <see cref="Missing"/> と <see cref="Unavailable"/> を潰してはならない。</strong>
/// 潰すとネットワーク断が「include 先が消えた」として伝播し、
/// 欠落したページを<em>正常な出力として</em>書き出す。前者はコンテンツの誤りで決定的、
/// 後者は環境の誤りでありキャッシュを汚してはならない。
/// </para>
/// </remarks>
// 派生型を入れ子にすることで閉じた階層を表現している。外に出すと利用者が任意の派生型を
// 追加できてしまい、網羅的なパターン マッチが成立しなくなる。
#pragma warning disable CA1034 // Nested types should not be visible
public abstract record ContentSourceResult
{
    private ContentSourceResult()
    {
    }

    /// <summary>
    /// 取得した。
    /// </summary>
    /// <param name="Source">取得された内容。</param>
    /// <param name="Validator">次回に取り直しの必要を判断するための検証子。</param>
    /// <param name="Stability">アドレスが一意な内容を指すかどうかの分類。</param>
    /// <remarks>
    /// <see cref="Fingerprint"/> は内容から計算する。
    /// <paramref name="Validator"/> から導いてはならない（<see cref="SourceValidator"/> 参照）。
    /// </remarks>
    public sealed record Fresh(
        ContentSource Source,
        SourceValidator Validator,
        SourceStability Stability) : ContentSourceResult;

    /// <summary>
    /// 取り直しは不要である。
    /// </summary>
    /// <param name="Validator">更新された検証子。</param>
    /// <remarks>
    /// <para>
    /// 下流は前回の <see cref="Fingerprint"/> と更新時刻を据え置く（= early cutoff）。
    /// </para>
    /// <para>
    /// <strong>これは「通信して確認した結果、変わっていなかった」を意味しない。</strong>
    /// 「変わっていないと判断した」だけである。HTTP の <c>Cache-Control: max-age</c> や
    /// <c>Expires</c> が鮮度期間内であれば、プロバイダは<em>ネットワークに一切触れずに</em>
    /// これを返せる（条件付き GET すら要らない）。この判断はプロバイダの私事なので、
    /// <strong>鮮度の概念を抽象に出す必要はない。</strong>
    /// <see cref="IContentSourceProvider.OpenAsync"/> は常に呼ばれ、
    /// 通信するかどうかはプロバイダが内部で決める。
    /// </para>
    /// <para>
    /// <see cref="SourceStability"/> を持たないのは、内容を取得していないためである。
    /// 分類は取得のたびに行うものなので、据え置かれた前回の分類がそのまま有効である。
    /// </para>
    /// </remarks>
    public sealed record Unchanged(
        SourceValidator Validator) : ContentSourceResult;

    /// <summary>
    /// 参照先が存在しない。
    /// </summary>
    /// <remarks>
    /// <strong>コンテンツの誤りである。</strong> <see cref="Diagnostic"/> にする。
    /// 決定的なので、この結果はキャッシュしてよい（存在しないこと自体が入力である）。
    /// </remarks>
    public sealed record Missing : ContentSourceResult
    {
        /// <summary>
        /// 唯一のインスタンス。
        /// </summary>
        /// <remarks>
        /// 追加の情報を持たないので、都度生成する意味がない。
        /// </remarks>
        public static Missing Instance { get; } = new();
    }

    /// <summary>
    /// 取得を試みたが到達できなかった。
    /// </summary>
    /// <param name="Reason">到達できなかった事由。診断に出すため、利用者に読める形で渡す。</param>
    /// <param name="Cause">元になった例外。無い場合は <see langword="null"/>。</param>
    /// <remarks>
    /// <para>
    /// <strong>環境の誤りである。キャッシュを汚してはならない。</strong>
    /// 接続不能、タイムアウト、通信を禁じた設定で内容がストアに無い場合。
    /// 「存在しない」ことを確認できていないのだから、次のビルドでは改めて試みる必要がある。
    /// </para>
    /// <para>
    /// <paramref name="Reason"/> を持たせているのは、これを受けて出す
    /// <see cref="Diagnostic"/> に事由が要るからである。中核はスキームを知らないので、
    /// 事由を書けるのはプロバイダだけである。
    /// </para>
    /// <para>
    /// <paramref name="Cause"/> はログのためのものであり、
    /// この値の同一性の判断や利用者向けメッセージには用いない。
    /// </para>
    /// </remarks>
    public sealed record Unavailable(
        string Reason,
        Exception? Cause = null) : ContentSourceResult;
}
#pragma warning restore CA1034
