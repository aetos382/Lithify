using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lithify.Hosting;

/// <summary>
/// Lithify のサイトを構成するビルダー。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IServiceCollection"/> と <see cref="IConfiguration"/> を露出させる薄いラッパーである。
/// <c>IHttpClientBuilder</c> や <c>AuthenticationBuilder</c> と同じイディオムに揃えており、
/// 独自のホストを作らないため、設定（appsettings.json / 環境変数 / コマンドライン）・
/// ロギング・DI・Options パターンが標準の作法でそのまま使える。
/// </para>
/// <para>
/// 各プラグイン パッケージは、この型に対する拡張メソッド（<c>UseMarkdig()</c> /
/// <c>UseHandlebarsNet()</c> / <c>AddBlog()</c> 等）として自分を登録する。
/// これにより <c>Lithify.Hosting</c> がプラグインを知らなくても構成 DSL が伸びる。
/// </para>
/// </remarks>
public interface ILithifyBuilder
{
    /// <summary>
    /// サービス コレクションを取得する。
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// 構成を取得する。
    /// </summary>
    IConfiguration Configuration { get; }
}
