# WsFiler

C#、.NET 10、Avalonia で構築されたクロスプラットフォームの2ペインファイルマネージャーです。高速なキーボード操作を重視して設計されています。

[English](README.md)

## 機能

- 効率的なファイル管理のための2ペインレイアウト
- キーボードファースト — 日常的な操作のほとんどをマウス不要で実行可能
- コピー・移動・削除・リネーム（競合時の確認ダイアログ付き）
- テキストファイルプレビュー
- ファイル一覧のソート
- 日本語・英語 UI
- セッション復元
- ライト・ダーク・OS追従テーマ
- キーバインドのカスタマイズ

## 対応プラットフォーム

Windows、macOS、Linux

## 必要環境

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## ビルド & 実行

```bash
# ビルド
dotnet build src/WsFiler.slnx

# 実行
dotnet run --project src/WsFiler.App/WsFiler.App.csproj

# テスト
dotnet test src/WsFiler.slnx
```

## NativeAOT ビルド（Windows x64）

実行前に `vswhere.exe` を PATH に追加してください（通常は `C:\Program Files (x86)\Microsoft Visual Studio\Installer`）。または Visual Studio の開発者コマンドプロンプトから実行してください。

```bash
dotnet publish src/WsFiler.App/WsFiler.App.csproj \
  -r win-x64 -c Release \
  -p:PublishAot=true --self-contained true
```

出力先: `src/WsFiler.App/bin/Release/net10.0/win-x64/publish/`

## デフォルトキーバインド

| キー | 操作 |
|------|------|
| `↑` / `↓` | カーソル移動 |
| `←` / `→` | ペイン対応の左右移動 |
| `PageUp` / `PageDown` | ページ上下 |
| `Home` / `End` | 先頭 / 末尾へ移動 |
| `Enter` | ディレクトリを開く / ファイルをプレビュー |
| `Backspace` | 親ディレクトリへ移動 |
| `Tab` | アクティブペインの切り替え |
| `Space` | 選択のトグル |
| `A` | 全選択 |
| `U` | 全選択解除 |
| `Escape` | ダイアログのキャンセル / 選択解除 |
| `C` | 非アクティブペインへコピー |
| `M` | 非アクティブペインへ移動 |
| `D` | 削除（確認あり） |
| `R` | 現在のアイテムをリネーム |

キーバインドは `settings.json` でカスタマイズできます。

## アーキテクチャ

WsFiler は 4 層のクリーンアーキテクチャを採用しています。

| 層 | プロジェクト | 責務 |
|----|-------------|------|
| UI | `WsFiler.App` | Avalonia ビュー、ダイアログ、エントリポイント |
| ViewModel | `WsFiler.Presentation` | MVVM ビューモデル、ダイアログ調整 |
| ドメイン | `WsFiler.Core` | コマンド、キーマップ、ファイルモデル、ペイン状態（Avalonia 非依存） |
| インフラ | `WsFiler.Infra` | ファイルシステムアクセス、設定永続化、ロギング |

詳細なアーキテクチャ仕様は [`docs/basic-design.md`](docs/basic-design.md) を参照してください。
