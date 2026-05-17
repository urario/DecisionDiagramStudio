# Decision Diagram Studio

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11%20x64-blue?logo=windows)](https://github.com/urario/DecisionDiagramStudio)
[![Framework](https://img.shields.io/badge/Framework-WinUI%203%20%2F%20.NET%208-purple)](https://learn.microsoft.com/windows/apps/winui/winui3/)
[![Language](https://img.shields.io/badge/Language-C%23%2012-green?logo=csharp)](https://learn.microsoft.com/dotnet/csharp/)
[![License: MIT](https://img.shields.io/badge/License-MIT-lightgrey)](LICENSE)


**BDD / ZDD / MTBDD / ZMTBDD を対話的に学習・実験・可視化する Windows デスクトップアプリです。**

Decision Diagram Studio は、Decision Diagram を手元で編集しながら構造の変化を確認するための学習・実験用ワークベンチです。BDD では真理値表を編集すると図が再構築され、削減前の BDT と削減後の BDD を切り替えて比較できます。

現在は開発中のプロトタイプです。バイナリ配布、CI、インストーラーはまだ整備中です。

## Demo

### BDD

https://github.com/user-attachments/assets/9d4254a6-95eb-4a3b-9cc0-1037a586f2e5



## Features

- **4 種類の Decision Diagram**: BDD / ZDD / MTBDD / ZMTBDD をワークベンチ上で切り替え
- **BDD 真理値表編集**: 0/1 セルをクリックして BDD を再構築
- **BDT / BDD 比較**: BDD セッションでは削減前 BDT と削減後 BDD を切り替え表示
- **ZDD 集合族入力**: `{a,b},{c}` 形式の集合族を入力し、Union / Intersect / Diff を実行
- **MTBDD / ZMTBDD 値テーブル**: 整数値テーブルから multi-terminal diagram を構築
- **BDD プリセット**: 2:1 multiplexer、Full adder、2-bit equality、4-bit one-hot を同梱
- **Graphviz + WebView2 表示**: `dot` が生成した SVG を WebView2 で表示し、パン・ズーム・ノードクリックに対応
- **解説パネル**: ノードクリック時に選択ノードの概要を表示
- **Undo / Redo**: ワークベンチ操作を最大 50 件まで記録
- **エクスポート**: 表を CSV としてクリップボードへコピー、図を SVG / DOT として保存
- **安全寄りの入力処理**: 変数名は ASCII 識別子に制限し、Graphviz には DOT を stdin で渡します

## Requirements

| 要件 | 詳細 |
|---|---|
| OS | Windows 10 / Windows 11 x64 |
| Target | `net8.0-windows10.0.19041.0` |
| Build tools | Visual Studio 2022 推奨。「.NET デスクトップ開発」と「Windows アプリ開発」ワークロードを入れてください |
| .NET | .NET 8 SDK |
| WebView2 Runtime | SVG プレビューに必要 |
| Graphviz | 図のプレビューと SVG 保存に必要。`dot.exe` を PATH または一般的な Graphviz インストール先から自動検出します |
| Git | submodule 取得に必要 |

## Installation

リリースバイナリはまだ公開していません。

公開後は [GitHub Releases](https://github.com/urario/DecisionDiagramStudio/releases) からダウンロードできるようにする予定です。現時点ではソースからビルドしてください。

## Build

Visual Studio 2022 の Developer PowerShell からの実行を推奨します。

```powershell
git clone --recurse-submodules https://github.com/urario/DecisionDiagramStudio.git
cd DecisionDiagramStudio
```

既に clone 済みで `lib/DecisionDiagramSharp` が空の場合:

```powershell
git submodule update --init --recursive
```

テスト:

```powershell
dotnet test tests/DecisionDiagramStudio.Tests/DecisionDiagramStudio.Tests.csproj -c Debug
```

アプリのビルド:

```powershell
dotnet build src/DecisionDiagramStudio/DecisionDiagramStudio.csproj -c Debug -p:Platform=x64
```

実行:

```powershell
dotnet run --project src/DecisionDiagramStudio/DecisionDiagramStudio.csproj -c Debug -p:Platform=x64
```

通常の PowerShell で WinUI 関連の `AppxPackage` / `Pri` タスクが見つからない場合は、Visual Studio 2022 の Windows アプリ開発ワークロードが入っているか確認してください。

## Usage

1. アプリを起動すると Workbench が開きます。
2. `Variables` に `a, b, c` のような変数名を入力し、`Apply variables` を押します。
3. `Family` で BDD / ZDD / MTBDD / ZMTBDD を選びます。
4. BDD では真理値表の値セルをクリックして 0/1 を切り替えます。
5. ZDD では `{a,b},{c}` のように集合族を入力して `Build ZDD` を押します。
6. MTBDD / ZMTBDD では整数値テーブルを編集してビルドします。
7. 図のノードをクリックすると、上部の解説パネルにノード情報が表示されます。
8. `Show BDT` / `Show BDD` で BDD の削減前後を切り替えます。
9. `Copy CSV`、`Save SVG`、`Save DOT` で結果を出力します。

## Input Formats

| Family | 入力 |
|---|---|
| BDD | 0/1 真理値表 |
| ZDD | 集合族テキスト。例: `{a,b},{c}` |
| MTBDD | 整数値テーブル |
| ZMTBDD | 整数値テーブル |

変数名は `^[a-zA-Z_][a-zA-Z0-9_]*$` に一致する ASCII 識別子のみ対応しています。

## Graphviz

Graphviz はアプリ本体の起動には必須ではありませんが、図の SVG プレビューと SVG 保存には必要です。

`dot.exe` は次の順で探索します。

- PATH
- `C:\Program Files\Graphviz\bin`
- `C:\Program Files (x86)\Graphviz\bin`
- `Graphviz*` 形式の一般的なインストール先

現在の UI には Graphviz パスを手動設定する画面はありません。Graphviz が見つからない場合、図のプレビューと SVG 保存は失敗しますが、DOT 保存は利用できます。

## Project Structure

```text
DecisionDiagramStudio/
├── DecisionDiagramStudio.slnx
├── LICENSE
├── README.md
├── docs/
├── lib/
│   └── DecisionDiagramSharp/        # git submodule
├── src/
│   └── DecisionDiagramStudio/
│       ├── App.xaml(.cs)
│       ├── MainWindow.xaml(.cs)
│       ├── Views/
│       ├── ViewModels/
│       ├── Services/
│       ├── Commands/
│       ├── Models/
│       ├── Infrastructure/
│       └── Assets/Presets/presets.json
└── tests/
    └── DecisionDiagramStudio.Tests/
```

## Development Notes

- UI は WinUI 3、アプリロジックは C# / .NET 8 です。
- Core の Decision Diagram 実装は `lib/DecisionDiagramSharp` submodule を参照しています。
- Nullable reference types と TreatWarningsAsErrors を有効にしています。
- テストは MSTest ベースです。

主な依存関係:

| Package | Version |
|---|---|
| Microsoft.WindowsAppSDK | 1.5.240802000 |
| Microsoft.Windows.SDK.BuildTools | 10.0.26100.1742 |
| CommunityToolkit.Mvvm | 8.3.2 |
| Microsoft.Extensions.DependencyInjection | 8.0.1 |
| Microsoft.Extensions.Logging | 8.0.1 |
| Serilog.Extensions.Logging | 8.0.0 |
| Serilog.Sinks.File | 5.0.0 |
| Serilog.Sinks.Debug | 2.0.0 |

## Roadmap

- CI とビルドバッジの追加
- GitHub Releases でのバイナリ配布
- Graphviz パスやテーマ設定の永続化
- CSV 以外の表エクスポート形式を UI から選択可能にする
- キーボードショートカット対応
- スクリーンショット / デモ GIF の追加
- インストーラーまたは MSIX 配布の整備

## Known Limitations

- 現時点では Windows x64 のみ対象です。
- CI とリリース成果物はまだありません。
- Graphviz がない場合、SVG プレビューと SVG 保存は使えません。
- 設定画面と `settings.json` 永続化は未実装です。
- UI からコピーできる表形式は現在 CSV のみです。
- BDT 表示は 10 変数までです。10 変数では終端込みで 2,047 ノードになります。
- ワークベンチの表編集は 10 変数までを想定しています。
- 日本語など Unicode の変数名には未対応です。

## License

This project is licensed under the [MIT License](LICENSE).

## Acknowledgements

- Core library: [DecisionDiagramSharp](https://github.com/urario/DecisionDiagramSharp)
- Graph rendering: [Graphviz](https://graphviz.org/)
- UI framework: WinUI 3 / Windows App SDK
- MVVM: CommunityToolkit.Mvvm
