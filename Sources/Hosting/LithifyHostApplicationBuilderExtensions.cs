using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Lithify.Hosting;

/// <summary>
/// <see cref="IHostApplicationBuilder"/> に Lithify を追加する拡張メソッド。
/// </summary>
public static class LithifyHostApplicationBuilderExtensions
{
    /// <summary>
    /// 構成のセクション名。
    /// </summary>
    private const string ConfigurationSectionName = "Lithify";

    /// <summary>
    /// Lithify のサービスを登録し、構成用のビルダーを返す。
    /// </summary>
    /// <param name="builder">ホストのビルダー。</param>
    /// <returns>Lithify を構成するビルダー。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> が <see langword="null"/> である。</exception>
    /// <remarks>
    /// <para>
    /// 複数回呼び出しても安全である。登録には <c>TryAdd</c> 系を使っているので、
    /// 2 回目以降は同じ論理的な構成に対するビルダーを返すだけになる。
    /// </para>
    /// <para>
    /// パーサー・レンダラー・テンプレート エンジンは登録されない。それらは
    /// <c>UseMarkdig()</c> のようにプラグイン側の拡張メソッドで明示的に足す。
    /// 既定で何かを登録すると「差し替え可能」という建前が崩れる。
    /// </para>
    /// <para>
    /// <c>Lithify</c> セクションから <see cref="LithifyOptions"/> を束縛する。
    /// コマンドライン引数による上書きは <c>RunLithifyAsync()</c> の中で
    /// <see cref="IPostConfigureOptions{TOptions}"/> として適用される。
    /// </para>
    /// </remarks>
    public static ILithifyBuilder UseLithify(
        this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;

        services.AddOptions<LithifyOptions>()
            .Bind(builder.Configuration.GetSection(ConfigurationSectionName));

        // コマンドライン上書きは実体としても IPostConfigureOptions としても解決される。
        // 前者はコマンドのアクションが値を書き込むため、後者は Options が読み出すため。
        // 同一インスタンスでなければならないので、後者は前者へ委譲する。
        services.TryAddSingleton(static _ => new CommandLineOverrides());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPostConfigureOptions<LithifyOptions>, CommandLineOverrides>(
                static provider => provider.GetRequiredService<CommandLineOverrides>()));

        // ファクトリーを明示するのは TryAddEnumerable が重複排除に実装型を要るため。
        // 型引数だけの登録にすると実装型が推論できず、登録が例外になる。
        services.TryAddEnumerable(
        [
            ServiceDescriptor.Singleton<ILithifyCommandProvider, BuildCommandProvider>(
                static provider => new BuildCommandProvider(provider)),
            ServiceDescriptor.Singleton<ILithifyCommandProvider, CleanCommandProvider>(
                static provider => new CleanCommandProvider(provider)),
        ]);

        return new LithifyBuilder(services, builder.Configuration);
    }
}
