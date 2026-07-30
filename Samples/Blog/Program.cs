using Lithify.Hosting;

using Microsoft.Extensions.Hosting;

// サイトの構成は C# コードで書く（R5）。ビルドすると、ソースとテンプレートを入力として
// HTML を出力する CLI ができる。
//
// パーサー・レンダラー・テンプレート エンジンの登録（UseMarkdig() 等）はまだ存在しない。
// それらのプラグイン パッケージが揃った時点でここに足す。
var builder = Host.CreateApplicationBuilder(args);

_ = builder.UseLithify();

return await builder.Build().RunLithifyAsync().ConfigureAwait(false);
