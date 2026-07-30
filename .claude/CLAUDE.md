# CLAUDE.md

## Pull Request の作成

- Renovate が作成した issue や PR を `close #xx` / `fix #xx` 等のキーワードで参照しない。

## 即時実行コマンド

Python は `python` も `python3` も禁止。
スクリプトを生成して実行する場合は、以下のいずれかのうち、現在の環境で使用可能なものを使用する。
- PowerShell
- C# (dotnet run)
- JavaScript (node.js)
- bash

## テストの実行

テストプロジェクトは全て MSTest.Sdk（Microsoft.Testing.Platform, MTP）。VSTest とはコマンドライン引数の扱いが異なる。

- テストのフィルタは `--` の後に渡す: `dotnet test <csproj> --framework net10.0 -- --filter "FullyQualifiedName~Xxx"`
  - `--` を付けずに `dotnet test ... --filter "..."` とすると **フィルタが MTP ランナーに渡らず、0 件マッチでエラーも出さず静かに終わる**（テストが通ったように見えるので注意）。
- フィルタ式自体は VSTest と同じ（`FullyQualifiedName~`, `TestCategory=` 等）。

## 設計上の不変条件

変更する際は [docs/architecture.md](../docs/architecture.md) を読むこと。以下は破ってはならない。

- **`Lithify.Abstractions` は具体的なエンジンに依存しない。** Markdig / AdocNet / YamlDotNet / TextMateSharp / テンプレート エンジンへの参照を追加してはならない。
- **`Lithify.Renderers.Html` は `ISyntaxHighlighter` を抽象経由でのみ使う。** `Lithify.Highlighting.TextMate` を参照してはならない。
- **char → UTF-8 の境界はフラグメント生成時の1回だけ。** 生成側は `TextWriter`、キャッシュ・合成・出力側は UTF-8 バイト列。`RenderedFragment` に `string` を持たせると全ページ合成のたびに再エンコードが走り、フラグメント合成の意義（R8）が失われる。
- **出力の判断と I/O は分離する。** 「内容が同じなら書かない」判断は純粋関数 `Decide` に置き、`IOutputStore` は I/O 境界に留める。
- **`Lithify.Testing` はテスト フレームワークに依存しない。** MSTest に依存する契約テスト基底クラスは `Tests/` 側に置く。
