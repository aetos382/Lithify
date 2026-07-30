---
paths:
  - '**/*.slnx'
  - '**/*.csproj'
  - '/Directory.Packages.props'
  - '/.config/dotnet-tools.json'
---

# .NET に関するルール

## `dotnet` コマンドの使用
- プロジェクトの操作は可能な限り適切な `dotnet` コマンドを通じて行う。
  - プロジェクトの作成は `dotnet new` コマンドおよび `dotnet solution add` コマンドを使用する。
  - プロジェクトへの NuGet パッケージ参照の追加は `dotnet package add` コマンド、削除は `dotnet package remove` コマンドを使用する。
  - プロジェクトへのプロジェクト参照の追加は `dotnet reference add` コマンド、削除は `dotnet reference remove` コマンドを使用する。
- これらのコマンドを使用することで達成できる目標については、*.csproj ファイルや Directory.Packages.props ファイルを直接編集してはならない。

## ローカルツールマニフェスト（`.config/dotnet-tools.json`）に関するルール

- `.config/dotnet-tools.json` は手書きしない。以下のコマンドを使用する。
  - マニフェストの新規作成: `dotnet new tool-manifest -o .config`
  - ツールの追加: `dotnet tool install --local <パッケージ名> --version <バージョン>`
  - ツールの削除: `dotnet tool uninstall --local <パッケージ名>`
  - ツールのバージョン更新: `dotnet tool update --local <パッケージ名> --version <バージョン>`

## `dotnet` コマンドで達成できない目標

- `dotnet` コマンドがサポートしていない操作に関しては、ファイルを直接変更する。
- `dotnet package remove` コマンドでパッケージ参照を削除した際、そのパッケージをソリューション内の他のプロジェクトで使用していない場合は、`Directory.Packages.props` ファイルからも削除する。
- git の競合解消など通常のパッケージ参照の追加・更新でない場合は直接編集してもよい。

## このリポジトリ固有の事項

- アセンブリ名は `Directory.Build.props` が `Lithify.$(MSBuildProjectName)` を自動設定する。プロジェクト名は `Parsers.Markdig` のように `Lithify.` を**付けずに**作る。
- NativeAOT はサポートしない（Handlebars.Net / Fluid / Blazor `HtmlRenderer` がリフレクションを使う）。`PublishAot` / `IsAotCompatible` を設定しない。
- Source Generator は存在しない。`IsRoslynComponent` を設定しない。
- ターゲットは `net10.0` 単一。`netstandard2.0` へのマルチターゲットはしない。
