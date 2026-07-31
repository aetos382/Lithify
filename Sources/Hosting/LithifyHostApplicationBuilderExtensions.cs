using System;

using Lithify.Abstractions;
using Lithify.Core.Content;
using Lithify.Core.Metadata;

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
    /// ただし<em>登録されたパーサーを索引する側</em>（<see cref="IContentFormatRegistry"/>）は
    /// 既定で登録する。特定のパーサーを知らないので建前は崩れず、
    /// 登録順が優先順になるという規則の持ち主が 1 箇所に定まる。
    /// 拡張子の対応表を変えるには <see cref="ContentFormatMap"/> を DI に登録する。
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

        // 別名の設定は構成から束縛しない（理由は ConfigureMetadataAliases に記す）。
        // それでも既定で登録するのは、各パーサーが IOptions<MetadataAliasOptions> を
        // 必須の依存として受けるためである。利用者が設定を書かなければ空の設定が渡り、
        // 各パーサーは自分の既定をそのまま使う。
        services.AddOptions<MetadataAliasOptions>();

        // コマンドライン上書きは実体としても IPostConfigureOptions としても解決される。
        // 前者はコマンドのアクションが値を書き込むため、後者は Options が読み出すため。
        // 同一インスタンスでなければならないので、後者は前者へ委譲する。
        services.TryAddSingleton(static _ => new CommandLineOverrides());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPostConfigureOptions<LithifyOptions>, CommandLineOverrides>(
                static provider => provider.GetRequiredService<CommandLineOverrides>()));

        // 形式のディスパッチは既定で登録してよい。この型は特定のパーサーを知らず、
        // 登録された IContentParser を索引するだけなので、「既定で何かを登録すると
        // 差し替え可能という建前が崩れる」に当たらない。むしろ各パーサー パッケージが
        // 自分でこれを登録すると、登録順に依存する優先順が誰の責務か曖昧になる。
        services.TryAddSingleton<IContentFormatRegistry>(
            static provider => new ContentFormatRegistry(
                provider.GetServices<IContentParser>(),
                provider.GetService<ContentFormatMap>()));

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
