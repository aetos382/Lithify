using System;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lithify.Hosting;

/// <summary>
/// <see cref="ILithifyBuilder"/> の既定の実装。
/// </summary>
/// <remarks>
/// 状態を持たない。<c>IHostApplicationBuilder</c> の
/// <see cref="IServiceCollection"/> と <see cref="IConfiguration"/> をそのまま通すだけである。
/// ビルダーに構成を溜め込むと <c>Build()</c> より前でしか設定できないものが生まれ、
/// Options パターンの再読み込みと衝突する。
/// </remarks>
internal sealed class LithifyBuilder :
    ILithifyBuilder
{
    public LithifyBuilder(
        IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        this.Services = services;
        this.Configuration = configuration;
    }

    public IServiceCollection Services { get; }

    public IConfiguration Configuration { get; }
}
