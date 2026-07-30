using System;

using Microsoft.Extensions.DependencyInjection;

namespace Lithify.Hosting;

/// <summary>
/// プラグインが登録すべきサービスの解決。
/// </summary>
/// <remarks>
/// <see cref="ServiceProviderServiceExtensions.GetRequiredService{T}(IServiceProvider)"/> が
/// そのまま投げる例外は「型が登録されていない」までしか言わない。CLI の利用者にとって
/// これは実質「何をすればよいか分からない」に等しい。<c>UseMarkdig()</c> のような
/// 拡張メソッドを呼ぶべきだと言えるのは Lithify 側だけなので、ここで言い換える。
/// </remarks>
internal static class ServiceResolution
{
    /// <summary>
    /// サービスを解決する。登録されていない場合は対処を示す例外を投げる。
    /// </summary>
    /// <typeparam name="T">解決するサービスの型。</typeparam>
    /// <param name="services">サービス プロバイダー。</param>
    /// <returns>解決されたサービス。</returns>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> の実装が登録されていない。</exception>
    public static T GetRequiredPlugin<T>(
        this IServiceProvider services)
        where T : notnull
    {
        return services.GetService<T>() is { } service
            ? service
            : throw new InvalidOperationException(
                Messages.FormatNoServiceRegistered(typeof(T).FullName));
    }
}
