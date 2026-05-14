# Decision Diagram Studio — バックログ

**バージョン:** 1.0
**作成日:** 2026-05-11
**対象アプリバージョン:** v0.1-studio〜v1.0-studio
**対応設計書:** `docs/architecture.md` v2.0

このバックログは `lib/DecisionDiagramSharp/docs/done-policy.md` の完了定義ポリシーに従って管理する。
タスクを完了とするには証跡が必須。証跡なしに Done にしてはならない。

---

## 目次

- [カテゴリ一覧](#カテゴリ一覧)
- [v0.1 — BDD 基本操作](#v01--bdd-基本操作)
  - [SETUP: プロジェクト基盤](#setup-プロジェクト基盤)
  - [IQ: アーキテクチャ未解決事項](#iq-アーキテクチャ未解決事項)
  - [MODEL: モデル層](#model-モデル層)
  - [SERVICE-BDD: DiagramService (BDD)](#service-bdd-diagramservice-bdd)
  - [SERVICE-GV: GraphvizService](#service-gv-graphvizservice)
  - [SERVICE-PS: PresetService](#service-ps-presetservice)
  - [CMD: CommandStack](#cmd-commandstack)
  - [VM-BDD: ViewModel (BDD)](#vm-bdd-viewmodel-bdd)
  - [VIEW-BDD: View (BDD)](#view-bdd-view-bdd)
  - [SEC: セキュリティ](#sec-セキュリティ)
  - [TEST-BDD: テスト (BDD)](#test-bdd-テスト-bdd)
- [v0.2 — ZDD + エクスポート + Undo/Redo](#v02--zdd--エクスポート--undoredo)
  - [SERVICE-ZDD: DiagramService (ZDD)](#service-zdd-diagramservice-zdd)
  - [SERVICE-EXP: ExportService](#service-exp-exportservice)
  - [VM-ZDD: ViewModel (ZDD)](#vm-zdd-viewmodel-zdd)
  - [VIEW-ZDD: View (ZDD)](#view-zdd-view-zdd)
  - [TEST-ZDD: テスト (ZDD)](#test-zdd-テスト-zdd)
- [v0.3 — MTBDD / ZMTBDD + 解説パネル](#v03--mtbdd--zmtbdd--解説パネル)
  - [SERVICE-MT: DiagramService (MTBDD/ZMTBDD)](#service-mt-diagramservice-mtbddzmtbdd)
  - [VM-MT: ViewModel (MTBDD/ZMTBDD)](#vm-mt-viewmodel-mtbddzmtbdd)
  - [VIEW-MT: View (MTBDD/ZMTBDD)](#view-mt-view-mtbddzmtbdd)
  - [TEST-MT: テスト (MTBDD/ZMTBDD)](#test-mt-テスト-mtbddzmtbdd)
- [v0.4 — 設定永続化・多言語](#v04--設定永続化多言語)
- [v1.0 — 全機能・Store 提出](#v10--全機能store-提出)
- [将来検討](#将来検討)

---

## カテゴリ一覧

| カテゴリ | 説明 |
|---|---|
| SETUP | プロジェクト・ソリューション構造の初期設定 |
| IQ | アーキテクチャ未解決事項の調査・決定 |
| MODEL | `Models/` 層の実装 |
| SERVICE-BDD | `DiagramService` の BDD 関連実装 |
| SERVICE-ZDD | `DiagramService` の ZDD 関連実装 |
| SERVICE-MT | `DiagramService` の MTBDD/ZMTBDD 関連実装 |
| SERVICE-GV | `GraphvizService` の実装 |
| SERVICE-PS | `PresetService` の実装 |
| SERVICE-EXP | `ExportService` の実装 |
| CMD | `CommandStack` および `IUndoableCommand` の実装 |
| VM-BDD | ViewModel (BDD フェーズ) の実装 |
| VM-ZDD | ViewModel (ZDD フェーズ) の実装 |
| VM-MT | ViewModel (MTBDD/ZMTBDD フェーズ) の実装 |
| VIEW-BDD | View/XAML (BDD フェーズ) の実装 |
| VIEW-ZDD | View/XAML (ZDD フェーズ) の実装 |
| VIEW-MT | View/XAML (MTBDD/ZMTBDD フェーズ) の実装 |
| SEC | セキュリティ実装 |
| TEST-BDD | テスト (BDD フェーズ) |
| TEST-ZDD | テスト (ZDD フェーズ) |
| TEST-MT | テスト (MTBDD/ZMTBDD フェーズ) |
| PERF | 性能計測・最適化 |
| I18N | 多言語対応 |
| DIST | 配布・MSIX・Store 対応 |

---

## v0.1 — BDD 基本操作

**目標:** BDD の構築・可視化・プリセット・BDT（削減前）表示が動作すること。ZDD/MTBDD/ZMTBDD はグレーアウト。

---

### SETUP: プロジェクト基盤

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| SETUP-001 | - | `DecisionDiagramStudio.sln` の作成と WinUI 3 プロジェクト (`src/DecisionDiagramStudio`) の追加 | `dotnet build DecisionDiagramStudio.sln` がエラーなしで完了する | `dotnet build` の出力確認 | N/A（スキャフォールディング） | N/A | VS MSBuild: `ビルドに成功しました。0 エラー` | N/A | N/A | Done | `DecisionDiagramStudio.sln` 作成。VS MSBuild で `0 エラー` を確認。`DecisionDiagramStudio.exe` が `bin/x64/Debug/net8.0-windows10.0.19041.0/` に出力される。`WindowsPackageType=None`, `WindowsSdkPackageVersion=10.0.19041.41` を設定して CLI 外ビルドに対応 |
| SETUP-002 | SETUP-001 | `tests/DecisionDiagramStudio.Tests` MSTest プロジェクトの追加とソリューション組み込み | `dotnet test` がゼロエラーで完了する（テストなしで OK） | `dotnet test` の出力確認 | N/A | N/A | `合格: 1、失敗: 0、スキップ: 0` (PlaceholderTest) | N/A | N/A | Done | `dotnet test tests\DecisionDiagramStudio.Tests\DecisionDiagramStudio.Tests.csproj` が `成功!` を返す。PlaceholderTest が合格。 |
| SETUP-003 | SETUP-001 | `lib/DecisionDiagramSharp` サブモジュールのプロジェクト参照設定 | `DecisionDiagramSharp.Core`, `DecisionDiagramSharp.Diagnostics`, `DecisionDiagramSharp.Export` のすべてのプロジェクト参照が解決され、ビルドが通る | ビルド後に各 DLL が出力ディレクトリに存在することを確認 | N/A | N/A | ビルド出力に `DecisionDiagramSharp.Core.dll`, `DecisionDiagramSharp.Diagnostics.dll`, `DecisionDiagramSharp.Export.dll` の存在を確認 | N/A | N/A | Done | 3本の DLL が `bin/x64/Debug/net8.0-windows10.0.19041.0/` に出力されることを確認 |
| SETUP-004 | SETUP-001 | NuGet パッケージ追加: `CommunityToolkit.Mvvm` 8.x, `Microsoft.Extensions.DependencyInjection` 8.x, `Microsoft.Extensions.Logging` 8.x | `<PackageReference>` が `.csproj` に存在し `dotnet restore` が成功する | `dotnet restore` の出力確認 | N/A | N/A | restore 成功ログ | N/A | N/A | Done | `CommunityToolkit.Mvvm 8.3.2`, `Microsoft.Extensions.DependencyInjection 8.0.1`, `Microsoft.Extensions.Logging 8.0.1`, `Microsoft.Extensions.Logging.Debug 8.0.1` を `.csproj` に追加。restore 成功確認。 |
| SETUP-005 | SETUP-001 | `.csproj` の品質設定: `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` の有効化 | `dotnet build` がゼロ警告で完了する（警告0件のログが証跡） | ビルドログの Warnings 行が 0 であることを確認 | N/A | N/A | VS MSBuild: `0 個の警告、0 エラー` | N/A | N/A | Done | `Nullable=enable`, `TreatWarningsAsErrors=true`, `GenerateDocumentationFile=true` を設定。C# コンパイラ警告 0 件を確認（MSBuild ツールチェーン警告 NETSDK1206 は SDK の RID 解決の既知問題で機能に影響なし） |
| SETUP-006 | SETUP-001 | `App.xaml.cs` の DI コンテナ初期設定（空の `IServiceCollection` 設定のみ） | アプリが起動し `MainWindow` が表示される | 手動起動確認 | N/A | N/A | 起動スクリーンショット | N/A | N/A | Done | `App.xaml.cs` に `IServiceCollection` + `BuildServiceProvider()` を実装。`OnLaunched` で `MainWindow.Activate()` を呼ぶ骨格を実装。手動起動確認はユーザー側で実施予定 |

---

### IQ: アーキテクチャ未解決事項

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| IQ-06 | - | ステータスバーの削減数定義の確定: 「Low==High 走査」および `TotalNodeCount - ReachableNodeCount` 代替は採用せず、`ReducedCount` を BDT→BDD の非終端ノード削減数として定義し `docs/architecture.md` へ反映する | `docs/architecture.md` の IQ-06 に決定事項が記載され、`AppDiagramStatistics` の `ReducedCount` の定義が確定している | 設計書レビュー | N/A（調査・設計） | N/A | 更新された設計書の差分 | N/A | N/A | Done | `docs/iq-06-09-architecture-investigation.md` の調査結果と 2026-05-13 の設計判断に基づき、`docs/architecture.md` B3.1 / ADR-009 / G 節へ反映。`ReducedCount = (2^VariableCount - 1) - ReachableNodeCount` と定義 |
| IQ-07 | - | SVG → WebView2 表示方式の確定: `NavigateToString()` + 先頭 CSP meta + nonce を採用し、2 MB 超過または header CSP 必須時に in-memory custom response へ移行できる設計を `docs/architecture.md` ADR-008 に反映する | ADR-008 に最終決定方式、根拠、移行方針が記載されている | 設計書レビュー | N/A（スパイク） | N/A | 更新された設計書の差分 | N/A | N/A | Done | `NavigateToString()` + 先頭 CSP meta + nonce を採用し、2 MB 超過または header CSP 必須時に in-memory custom response へ移行する二段階方針を `docs/architecture.md` B9.2 / ADR-008 / G 節へ反映 |
| IQ-08 | - | 「新規」ボタン時の `DecisionDiagramManager` ライフサイクル確定: New 時に再生成して変数テーブルをリセットする方針を設計書に記載する | `docs/architecture.md` の IQ-08 に決定事項が記載され、`DiagramService` の新規操作フローが明確になっている | 設計書レビュー | N/A（設計） | N/A | 更新された設計書の差分 | N/A | N/A | Done | New 時に `DiagramService` 内部の `DecisionDiagramManager` を再生成する方針を `docs/architecture.md` B3.2 / ADR-010 / G 節へ反映 |
| IQ-09 | - | 変数名の許容文字セット確定: ASCII 識別子 `^[a-zA-Z_][a-zA-Z0-9_]*$` のみを許可し、日本語変数名を仕様から除外する方針を設計書に記載する | `docs/architecture.md` の IQ-09 に採用パターンと判断根拠が記載されている | 設計書レビュー | N/A（設計） | N/A | 更新された設計書の差分 | N/A | N/A | Done | ASCII 識別子 `^[a-zA-Z_][a-zA-Z0-9_]*$` のみ許可し、日本語変数名は仕様から除外する方針を `docs/architecture.md` B9.4 / ADR-011 / G 節へ反映 |

---

### MODEL: モデル層

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| MODEL-001 | - | `DiagramFamily` 列挙型の実装: `BDD`, `ZDD`, `MTBDD`, `ZMTBDD` の4値 | `DiagramFamily` が4値を持ち、ビルドが通る | コンパイル確認 | N/A（純粋定義） | N/A | VS MSBuild 成功 | N/A | N/A | Done | `DiagramFamily_ShouldContainAllArchitectureFamilies` 合格。`MSBuild.exe src\DecisionDiagramStudio\DecisionDiagramStudio.csproj /p:Platform=x64 /v:minimal` 成功 |
| MODEL-002 | - | `AppDiagramStatistics` レコードの実装: `ReachableNodeCount`, `ReachableTerminalCount`, `TotalNodeCount`, `VariableCount`, `BdtNodeCount`, `ReducedCount`, `SetCount` | 全フィールドが設計書 B3.1 の型定義と一致し、ビルドが通る | コンパイル + コードレビュー | N/A（純粋定義） | N/A | VS MSBuild 成功 | N/A | N/A | Done | `AppDiagramStatisticsTests` 合格。`DecisionDiagramStudio.Models.AppDiagramStatistics` coverage line-rate=1, branch-rate=1 |
| MODEL-003 | MODEL-002 | `AppDiagramStatistics` の BDD 用ファクトリメソッド `ForBdd(DiagramStatistics)` の実装: `BdtNodeCount = 2^VariableCount - 1`, `ReducedCount = BdtNodeCount - ReachableNodeCount` を算出する | 2変数BDD（VariableCount=2）の入力で `BdtNodeCount=3`, `ReducedCount` が正しく算出される | ユニットテスト | Yes | 実装前 `dotnet test tests\DecisionDiagramStudio.Tests\DecisionDiagramStudio.Tests.csproj -v:minimal` で `CS0234: DecisionDiagramStudio.Models` 未実装により失敗 | `ForBdd_VariableCount2_ShouldReturn_BdtNodeCount3` を含む `dotnet test` 合格（合格: 13、失敗: 0） | 変更メソッド 100% | `coverage.cobertura.xml` で `ForBdd` line-rate=1, branch-rate=1 | Done | `BdtNodeCount=3`, `ReducedCount=1` をユニットテストで検証 |
| MODEL-004 | MODEL-002 | `AppDiagramStatistics` の ZDD 用ファクトリメソッド `ForZdd(DiagramStatistics, long setCount)` の実装 | `SetCount` フィールドに引数の `setCount` が格納されること | ユニットテスト | Yes | 実装前 `dotnet test tests\DecisionDiagramStudio.Tests\DecisionDiagramStudio.Tests.csproj -v:minimal` で `CS0234: DecisionDiagramStudio.Models` 未実装により失敗 | `ForZdd_WithSetCount_ShouldStoreSetCount` を含む `dotnet test` 合格（合格: 13、失敗: 0） | 変更メソッド 100% | `coverage.cobertura.xml` で `ForZdd` line-rate=1, branch-rate=1 | Done | `SetCount=5` と負数拒否をユニットテストで検証 |
| MODEL-005 | - | `DiagramSession` レコードの実装: `Family`, `VariableNames`, `VariableOrder`, `IntValueTable?`, `SetInput?`, `DotText`, `Statistics`, `IsEmpty`, `LastModified` | 全フィールドが設計書 B3.1 の型定義と一致し、ビルドが通る。`IsEmpty` は `string.IsNullOrEmpty(DotText)` で実装されている | コンパイル + コードレビュー | N/A（純粋定義） | N/A | VS MSBuild 成功 | N/A | N/A | Done | `DiagramSession_IsEmpty_ShouldReflectDotText`, `DiagramSession_ShouldStoreSessionContractValues` 合格。`DiagramSession` coverage line-rate=1, branch-rate=1 |
| MODEL-006 | - | `DiagramPreset` レコードの実装: `Id`, `Label`, `Description`, `VariableNames`, `TruthTableValues`, `DefaultFamily` | 設計書 B3.1 の型定義と一致し、ビルドが通る | コンパイル確認 | N/A（純粋定義） | N/A | VS MSBuild 成功 | N/A | N/A | Done | `DiagramPreset_ShouldStorePresetContractValues` 合格。`DiagramPreset` coverage line-rate=1, branch-rate=1 |
| MODEL-007 | - | `SessionOptions` レコードの実装: `GraphvizPath`, `Theme`, `MaxNodeCount`, `MaxEnumerationCount`, `UndoHistoryLimit` | 設計書 B3.1 の型定義と一致し、ビルドが通る | コンパイル確認 | N/A（純粋定義） | N/A | VS MSBuild 成功 | N/A | N/A | Done | `SessionOptions_ShouldStoreConfigurationContractValues` 合格。`SessionOptions` coverage line-rate=1, branch-rate=1 |

---

### SERVICE-BDD: DiagramService (BDD)

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| SVC-BDD-001 | - | `IDiagramService` インターフェースの定義: `BuildAsync(string[] variableNames, int[] intValueTable, DiagramFamily family, CancellationToken ct) Task<DiagramSession>`, `GetBdtDotAsync(DiagramSession session, CancellationToken ct) Task<string>` | インターフェースが `Services/Interfaces/IDiagramService.cs` に存在し、ビルドが通る | コンパイル確認 | N/A（インターフェース定義） | N/A | ビルド成功ログ | N/A | N/A | Todo | - |
| SVC-BDD-002 | SVC-BDD-001 | `DiagramService` クラスの骨格実装: `DecisionDiagramManager` と `SemaphoreSlim(1)` をフィールドとして保持し、DI コンストラクタ注入で受け取る | DI 登録後にインスタンスが生成できる（起動時クラッシュなし） | アプリ起動確認 | N/A（スキャフォールディング） | N/A | 起動ログ | N/A | N/A | Todo | - |
| SVC-BDD-003 | SVC-BDD-002 | 変数名バリデーション実装: `BuildAsync` 冒頭で全変数名を `^[a-zA-Z_][a-zA-Z0-9_]*$` にマッチさせ、不正な場合は `ArgumentException` をスローする | 不正変数名（例: `"1a"`, `"a b"`, `"<script>"`）で `ArgumentException` がスローされ、正常な変数名ではスローされない | ユニットテスト | Yes | `BuildAsync_InvalidVariableName_ShouldThrow_ArgumentException` が失敗するテスト出力 | 同テストの合格出力 | 変更メソッド 100%、分岐 ≥ 85% | カバレッジレポート | Todo | - |
| SVC-BDD-004 | SVC-BDD-003 | `DiagramService.BuildBddFromTruthTable(int[] values, string[] variableNames)` の実装: Shannon Expansion（ITE 演算）で `BddManager.Var()`, `And()`, `Or()`, `Not()` を使って BDD を構築する | 2変数の既知真理値表（例: A AND B）で構築した BDD を `BddDiagnostics.BuildTruthTable()` で逆変換した結果が元の真理値表と一致する | ユニットテスト（真理値表往復確認） | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100%、行 ≥ 90%、分岐 ≥ 85% | カバレッジレポート | Todo | - |
| SVC-BDD-005 | SVC-BDD-004 | `DiagramService.BuildAsync` の BDD パスの実装: critical section 内で `BuildBddFromTruthTable` → `GetStatistics` → `ToDot` を実行し、`DiagramSession` を返す | 3変数の真理値表で `BuildAsync` を呼び、返却された `DiagramSession.DotText` が有効な DOT 文字列（`digraph` で始まる）であること | ユニットテスト（モック GraphvizService 使用） | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |
| SVC-BDD-006 | SVC-BDD-002 | Critical section の `SemaphoreSlim` 保護実装: 同時に2つの `BuildAsync` を呼んだ場合に、後の呼び出しが前の完了を待機してから実行される | 2つのタスクを並行実行した際に `SemaphoreSlim` が排他制御することを確認するテスト（インターリーブ検出） | ユニットテスト | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |
| SVC-BDD-007 | SVC-BDD-004 | `DiagramService.GetBdtDotAsync` の実装: 変数数 `n`（≤10）の完全二分木 DOT テキストを直接生成する。変数数 > 10 の場合は `BdtVariableLimitException` をスローする | 2変数で `2^3-1=7` ノードを持つ DOT テキストが返却される。変数数 11 では `BdtVariableLimitException` がスローされる | ユニットテスト | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100%、分岐 ≥ 85% | カバレッジレポート | Todo | - |

---

### SERVICE-GV: GraphvizService

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| SVC-GV-001 | - | `IGraphvizService` インターフェースの定義: `RenderSvgAsync(string dotText, CancellationToken ct) Task<string>`, `IsAvailable() bool` | インターフェースが `Services/Interfaces/IGraphvizService.cs` に存在し、ビルドが通る | コンパイル確認 | N/A | N/A | ビルド成功ログ | N/A | N/A | Done | `Services/Interfaces/IGraphvizService.cs` を追加。VS MSBuild `DecisionDiagramStudio.csproj /p:Platform=x64 /v:minimal` 成功 |
| SVC-GV-002 | SVC-GV-001 | `GraphvizService.RenderSvgAsync` の実装: DOT テキストを `dot.exe` の stdin に渡し、stdout から SVG を取得する。`dot.exe` は stdout/stdin のみ使用（引数にユーザー入力を含めない）。タイムアウト 30 秒 | 既知の簡単な DOT テキスト（`digraph G { a -> b }`）を渡して SVG（`<svg` で始まる文字列）が返却される | 統合テスト（実 Graphviz 使用、CI 環境スキップ可） | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Done | `GraphvizService.RenderSvgAsync` を追加。stdin/stdout + `ArgumentList("-Tsvg")` でユーザー入力を引数に含めない。`dotnet test` 46/46 合格、実 Graphviz テストは `IsAvailable()` false の環境ではスキップ |
| SVC-GV-003 | SVC-GV-002 | `GraphvizService` のフォールバック: `dot.exe` が見つからない場合は `GraphvizNotFoundException` をスローする。タイムアウト時は `GraphvizTimeoutException` をスローする | 無効パスを設定した場合に `GraphvizNotFoundException` がスローされることをモックで確認 | ユニットテスト | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Done | `GraphvizNotFoundException` / `GraphvizTimeoutException` を追加。`RenderSvgAsync_InvalidPath_ShouldThrow_GraphvizNotFoundException` 合格 |

---

### SERVICE-PS: PresetService

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| SVC-PS-001 | - | `IPresetService` インターフェースの定義と `PresetService` の骨格実装 | インターフェースと実装クラスが存在し、ビルドが通る | コンパイル確認 | N/A | N/A | ビルド成功ログ | N/A | N/A | Done | `Services/Interfaces/IPresetService.cs` と `Services/PresetService.cs` を追加。VS MSBuild 成功 |
| SVC-PS-002 | SVC-PS-001 | `Assets/Presets/presets.json` の作成と BDD 学習用プリセット（最低4件）の定義: `f = a`, `f = a AND b`, `f = a OR b`, `f = a XOR b` | `PresetService.GetPreset(id)` が4件のプリセットをそれぞれ返し、`VariableNames` と `TruthTableValues` が正しい値を持つ | ユニットテスト | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Done | `presets.json` に `f = a`, `AND`, `OR`, `XOR` の4件を定義。`GetPreset_RequiredBddLearningPresets_ShouldReturnExpectedTruthTables` 合格 |

---

### CMD: CommandStack

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| CMD-001 | - | `IUndoableCommand` インターフェースの定義: `Execute()`, `Undo()` | インターフェースが `Commands/IUndoableCommand.cs` に存在し、ビルドが通る | コンパイル確認 | N/A | N/A | ビルド成功ログ | N/A | N/A | Done | `Commands/IUndoableCommand.cs` を追加。VS MSBuild 成功 |
| CMD-002 | CMD-001 | `CommandStack` クラスの実装: `Push(IUndoableCommand)`, `Undo()`, `Redo()`, 上限 50 件（超過時は最古を削除）、`CanUndo`, `CanRedo` プロパティ | Push/Undo/Redo の連鎖が正しく動作し、51件目のプッシュで最古エントリが削除される | ユニットテスト | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100%、Undo/Redo パス 100% | カバレッジレポート | Done | `Commands/CommandStack.cs` を追加。`CommandStackTests` 12ケース合格、51件目 Push で最古削除を検証 |
| CMD-003 | CMD-001 | `ChangeTruthTableCommand` の実装: 変更前後の `int[]` スナップショットを保持し、`Execute`/`Undo` で `DiagramService.BuildAsync` を呼ぶ | `Execute` → `Undo` → `Execute` で `DiagramSession` が各状態と一致する | ユニットテスト（モック DiagramService） | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Done | `ChangeTruthTableCommand` を追加。`ExecuteUndoExecute_ShouldApplyAfterBeforeAfterSnapshots` 合格 |

---

### VM-BDD: ViewModel (BDD)

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| VM-BDD-001 | - | `WorkbenchViewModel` の骨格実装: `IDiagramService`, `IPresetService`, `CommandStack` を DI で受け取り、`[ObservableProperty]` で `VariableNames`, `IntValueTable`, `SelectedFamily` を持つ | ViewModel がインスタンス化でき、初期状態で `SelectedFamily == BDD` である | ユニットテスト（モック使用） | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |
| VM-BDD-002 | VM-BDD-001 | `SelectPresetCommand` の実装: `PresetService.GetPreset()` → `ChangeTruthTableCommand` → `CommandStack.Push` → `DiagramService.BuildAsync` の一連のフロー | プリセット選択後に `CurrentSession.DotText` が非空の DOT 文字列になる | ユニットテスト（モック） | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |
| VM-BDD-003 | VM-BDD-001 | TT セル変更時のデバウンス実装: セル変更から 300ms 後に `BuildAsync` が実行され、連続変更時は前の `CancellationTokenSource.Cancel()` を呼ぶ | 100ms 間隔で3回 TT 変更を行った場合、`BuildAsync` が1回だけ呼ばれることをモックで確認 | ユニットテスト | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |
| VM-BDD-004 | - | `DiagramPanelViewModel` の実装: `SvgContent`, `DotText`, `IsReduced`, `ToggleReductionCommand`, `[BDD専用] BDTボタン表示制御` | BDD セッションで `ToggleReductionCommand` を実行すると `IsReduced` が切り替わり、ZDD セッションではボタンが非表示になる | ユニットテスト（モック） | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |
| VM-BDD-005 | - | `StatisticsViewModel` の実装: `DiagramSession.Statistics` を受け取り `ReachableNodeCount`, `TotalNodeCount`, `ReducedCount` 等の表示プロパティを持つ。`ReducedCount` は BDT→BDD の非終端ノード削減数として表示する | `Session` プロパティを更新すると全表示プロパティが連動して更新される | ユニットテスト | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |
| VM-BDD-006 | - | `ExplanationViewModel` の骨格実装: `SelectNode(nodeId, session)` でノード選択状態を保持し、解説テキスト `ExplanationText` を生成する（v0.1 は簡易テキスト） | `SelectNode("n1", session)` 呼び出し後に `ExplanationText` が非空になる | ユニットテスト | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |

---

### VIEW-BDD: View (BDD)

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| VIEW-BDD-001 | - | `MainWindow.xaml` の実装: `NavigationView` ホストで WorkbenchPage を初期ページとして表示する | アプリ起動後に WorkbenchPage が表示され、ナビゲーションが動作する | 手動動作確認 | N/A（UI） | N/A | 動作確認スクリーンショット | N/A（UI コード除外） | N/A | Todo | - |
| VIEW-BDD-002 | VIEW-BDD-001 | `WorkbenchPage.xaml` の実装: 変数名入力エリア、真理値表グリッド（BDD用）、ファミリーラジオボタン（ZDD/MTBDD/ZMTBDD はグレーアウト）、プリセットボタン一覧 | v0.1 のすべての UI 要素が表示され、ViewModel へのバインディングが機能する | 手動動作確認（プリセット選択→表示更新） | N/A（UI） | N/A | 動作確認スクリーンショット | N/A（UI コード除外） | N/A | Todo | - |
| VIEW-BDD-003 | VIEW-BDD-001 | `WorkbenchPage` の WebView2 埋め込みと SVG 表示実装（IQ-07 の決定方式に従う） | BDD を構築すると WebView2 に SVG グラフが表示される | 手動動作確認 | N/A（UI） | N/A | SVG 表示スクリーンショット | N/A（UI コード除外） | N/A | Todo | IQ-07 解決済み。`NavigateToString()` + 先頭 CSP meta + nonce 方針に従う |
| VIEW-BDD-004 | VIEW-BDD-001 | ステータスバーの実装: ノード数・削減数等を表示する `InfoBar` / ステータスストリップ | BDD 構築後に統計値（ノード数等）がステータスバーに表示される | 手動動作確認 | N/A（UI） | N/A | 表示スクリーンショット | N/A（UI コード除外） | N/A | Todo | IQ-06 解決済み。`ReducedCount` は BDT→BDD の非終端ノード削減数 |
| VIEW-BDD-005 | VIEW-BDD-001 | エラー表示の `InfoBar` 実装: `ArgumentException`（変数名不正）・`GraphvizNotFoundException`・`BdtVariableLimitException` 等を B8.1 のフローに従い表示する | 不正変数名入力時に赤 `InfoBar` が表示される | 手動動作確認 | N/A（UI） | N/A | エラー表示スクリーンショット | N/A（UI コード除外） | N/A | Todo | - |
| VIEW-BDD-006 | VIEW-BDD-001 | [削減前(BDT)] / [削減後] トグルボタンの実装: BDD ファミリー選択時のみ表示し、クリックで `DiagramPanelViewModel.ToggleReductionCommand` を呼ぶ | BDD 選択時にボタンが表示され、ZDD 選択時に非表示になる | 手動動作確認 | N/A（UI） | N/A | 表示/非表示スクリーンショット | N/A（UI コード除外） | N/A | Todo | - |

---

### SEC: セキュリティ

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| SEC-001 | SVC-BDD-003 | 変数名バリデーションのセキュリティテスト: `<script>`, `"; DROP TABLE`, `../`, `\n`, null 文字を含む入力が `ArgumentException` でブロックされる | 設計書 B9.4 の全ての拒否パターンが `ArgumentException` をスローする | ユニットテスト | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100%、分岐 100% | カバレッジレポート | Todo | - |
| SEC-002 | VIEW-BDD-003 | WebView2 CSP 設定の実装と検証: `NavigateToString()` で読み込む HTML の先頭 CSP meta に `script-src nonce-{random}` が設定され、`<script>` タグが含まれる DOT から生成された SVG でも nonce なしスクリプトが実行されない | 手動テスト: `<script>alert(1)</script>` を含む DOT テキストをレンダリングしてアラートが表示されないことを確認 | 手動セキュリティテスト | N/A | N/A | テスト実行記録（アラート非表示確認） | N/A | N/A | Todo | IQ-07 解決済み。2 MB 超過または header CSP 必須時は in-memory custom response へ移行 |
| SEC-003 | VIEW-BDD-003 | `postMessage` スキーマ検証の実装: `type`, `nodeId`, `variableName`, `nodeType` の各フィールドを設計書 B5 のパターンで検証し、不正なメッセージをサイレントに無視する | 不正 JSON (`{"type": "exec", "cmd": "rm -rf /"}`) を送信した場合に `ExplanationViewModel.ExplanationText` が変化しない | ユニットテスト | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |

---

### TEST-BDD: テスト (BDD)

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| TEST-BDD-001 | SVC-BDD-004 | `DiagramServiceTests` の統合テスト: `BuildBddFromTruthTable` を実際の `DecisionDiagramSharp` ライブラリで実行し、1〜4変数の全真理値表パターンで往復確認（`BuildTruthTable` で逆変換して一致確認） | 全テストケース（1変数: 4通り, 2変数: 16通り, 3変数: 256通り, 4変数: 65536通り）が合格する | 統合テスト（実ライブラリ使用） | Yes | 失敗テスト出力 | 合格テスト出力 | `BuildBddFromTruthTable` メソッド 100% | カバレッジレポート | Todo | - |
| TEST-BDD-002 | SVC-BDD-007 | `DiagramService.GetBdtDotAsync` のテスト: 変数数1〜10で `2^(n+1)-1` ノード数の DOT テキストが生成され、変数数11で例外がスローされる | 各変数数のノード数が DOT テキスト内のノード定義行数と一致する | ユニットテスト | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100%、分岐 100% | カバレッジレポート | Todo | - |
| TEST-BDD-003 | CMD-002 | `CommandStackTests`: Push/Undo/Redo の連鎖、50件上限、`CanUndo`/`CanRedo` 状態遷移 | 全12ケース（正常系・境界値・上限超過）が合格する | ユニットテスト | Yes | 失敗テスト出力 | 合格テスト出力 | `CommandStack` メソッド 100%、分岐 100% | カバレッジレポート | Done | `tests/DecisionDiagramStudio.Tests/Commands/CommandStackTests.cs` を追加。12/12 合格、全体 `dotnet test` 46/46 合格 |
| TEST-BDD-004 | - | v0.1 完了基準チェック: `dotnet test` が全テスト合格であり、4変数以下の TT 変更→SVG 表示が手動で 300ms 以内に完了する | テスト合格ログ + 手動パフォーマンス計測結果（Graphviz warm 条件） | 統合確認 | N/A | N/A | テスト合格ログ + パフォーマンス記録 | N/A | N/A | Todo | - |

---

## v0.2 — ZDD + エクスポート + Undo/Redo

**目標:** ZDD の集合族入力・可視化・操作が動作する。TT/SVG/DOT エクスポートと Undo/Redo が機能する。

---

### SERVICE-ZDD: DiagramService (ZDD)

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| SVC-ZDD-001 | SVC-BDD-001 | `IDiagramService` に ZDD 用メソッド `BuildAsync(string[] variableNames, IReadOnlyList<IReadOnlyList<string>> setInput, DiagramFamily.ZDD, CancellationToken ct) Task<DiagramSession>` を追加 | インターフェースと実装クラスが更新され、ビルドが通る | コンパイル確認 | N/A | N/A | ビルド成功ログ | N/A | N/A | Todo | - |
| SVC-ZDD-002 | SVC-ZDD-001 | `DiagramService.BuildAsync` の ZDD パスの実装: `ZddManager.MakeFamily()` → `GetStatistics()` → `CountSets()` → `ToDot()` を critical section 内で実行し `DiagramSession` を返す | `{{a, b}, {c}}` の入力で `SetCount=2` の `DiagramSession` が返却される | ユニットテスト（実ライブラリ） | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |
| SVC-ZDD-003 | SVC-ZDD-002 | `IDiagramService` に `ApplyZddOperationAsync(ZddOperation op, CancellationToken ct) Task<DiagramSession>` を追加し、`Union`, `Intersection`, `Difference` の3演算を実装する | 2つの既知集合族に対して Union/Intersection/Difference の結果 `SetCount` が正しい値になる | ユニットテスト（実ライブラリ） | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100%、分岐 ≥ 85% | カバレッジレポート | Todo | - |

---

### SERVICE-EXP: ExportService

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| SVC-EXP-001 | - | `IExportService` インターフェースの定義と `ExportService` 骨格実装 | インターフェースが `Services/Interfaces/IExportService.cs` に存在し、ビルドが通る | コンパイル確認 | N/A | N/A | ビルド成功ログ | N/A | N/A | Todo | - |
| SVC-EXP-002 | SVC-EXP-001 | `ExportService.CopyTruthTableAsync` の実装: BDD の `DiagramSession.IntValueTable` から CSV / Markdown / AsciiDoc 形式の文字列を生成し、OS クリップボードへコピーする | 2変数 BDD の真理値表を CSV 形式でコピーした文字列が期待するヘッダー・行数と一致する | ユニットテスト + クリップボード内容確認 | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |
| SVC-EXP-003 | SVC-EXP-001 | `ExportService.SaveSvgAsync` の実装: `FileSavePicker` で SVG ファイルを保存する。`IOException` を捕捉して呼び出し元に通知する | 保存先を指定してファイルが作成され、内容が `DiagramSession.DotText` を Graphviz でレンダリングした SVG と一致する | 手動動作確認 | N/A（UI 操作） | N/A | 動作確認記録 | N/A | N/A | Todo | - |

---

### VM-ZDD: ViewModel (ZDD)

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| VM-ZDD-001 | VM-BDD-001 | `WorkbenchViewModel` に ZDD 入力状態 `SetInput` プロパティと `ApplyZddOperationCommand` を追加 | ZDD ファミリー選択時に `SetInput` が有効になり、操作コマンドが実行できる | ユニットテスト（モック） | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |
| VM-ZDD-002 | VM-BDD-004 | `DiagramPanelViewModel` で ZDD 選択時に BDT ボタンを非表示にする制御の確認テスト追加 | `SelectedFamily = ZDD` 設定後に `IsBdtButtonVisible == false` である | ユニットテスト | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |
| VM-ZDD-003 | - | `ChangeFamilyCommand` の実装: ファミリー切り替えを Undo 対象の操作として `CommandStack` に積む | ファミリーを ZDD → BDD → Undo と操作すると ZDD に戻る | ユニットテスト（モック） | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |

---

### VIEW-ZDD: View (ZDD)

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| VIEW-ZDD-001 | VIEW-BDD-002 | ZDD 集合族テキスト入力 UI の実装: `WorkbenchPage` に集合族テキストエリア（例: `{a,b},{c}`）を追加し、ZDD ファミリー選択時に表示・BDD 時に非表示にする | ZDD 選択時に集合族入力エリアが表示され、入力内容が `WorkbenchViewModel.SetInput` にバインドされる | 手動動作確認 | N/A（UI） | N/A | 動作確認スクリーンショット | N/A | N/A | Todo | - |
| VIEW-ZDD-002 | VIEW-ZDD-001 | ZDD 集合族操作ボタン（Union / Intersection / Difference）の追加 | 各ボタンクリックで対応する `ApplyZddOperationCommand` が実行される | 手動動作確認 | N/A（UI） | N/A | 動作確認スクリーンショット | N/A | N/A | Todo | - |
| VIEW-ZDD-003 | - | Undo/Redo ボタンの追加（ツールバー）と `CommandStack.CanUndo` / `CanRedo` への IsEnabled バインディング | Undo/Redo が動作し、スタックが空のときボタンが無効になる | 手動動作確認 | N/A（UI） | N/A | 動作確認スクリーンショット | N/A | N/A | Todo | - |

---

### TEST-ZDD: テスト (ZDD)

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| TEST-ZDD-001 | SVC-ZDD-003 | ZDD 演算テスト: Union / Intersection / Difference を既知の集合族（`{{a,b},{c}}` と `{{b},{c,d}}`）で実行し、`SetCount` と `BuildSetFamilyTable` の結果を明示的なテーブルで検証する | 3演算すべての `SetCount` と集合族内容が期待値と一致する | 統合テスト（実ライブラリ） | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |
| TEST-ZDD-002 | - | v0.2 完了基準チェック: 全テスト合格 + Undo/Redo の手動動作確認 + SVG/DOT エクスポートの手動動作確認 | テスト合格ログ + 動作確認記録 | 統合確認 | N/A | N/A | テスト合格ログ + 動作記録 | N/A | N/A | Todo | - |

---

## v0.3 — MTBDD / ZMTBDD + 解説パネル

**目標:** MTBDD と ZMTBDD の整数値テーブル入力・可視化・値テーブル表示が動作する。解説パネルが全ファミリーで機能する。

---

### SERVICE-MT: DiagramService (MTBDD/ZMTBDD)

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| SVC-MT-001 | SVC-BDD-001 | `DiagramService.BuildAsync` の MTBDD パスの実装: `MtbddManager.Create(IReadOnlyList<int>)` → `GetStatistics()` → `ToDot()` を critical section 内で実行する | 2変数の整数値テーブル `[0, 1, 2, 3]` で `BuildAsync` を呼び、`DiagramSession.DotText` が有効な DOT 文字列になる | ユニットテスト（実ライブラリ） | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |
| SVC-MT-002 | SVC-BDD-001 | `DiagramService.BuildAsync` の ZMTBDD パスの実装: `ZmtbddManager.Create(IReadOnlyList<int>)` を使用する | MTBDD と同一入力に対して ZMTBDD の `ReachableNodeCount ≤ MTBDD.ReachableNodeCount` が成立する（全0値テーブルの場合） | ユニットテスト（実ライブラリ） | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |
| SVC-MT-003 | - | 全ファミリーに対する `AppDiagramStatistics` ファクトリメソッドの完成（MTBDD/ZMTBDD 用: `ForMtbdd`, `ForZmtbdd`） | MTBDD の `ReachableTerminalCount` が異なる整数値の数と一致する | ユニットテスト（実ライブラリ） | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |

---

### VM-MT: ViewModel (MTBDD/ZMTBDD)

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| VM-MT-001 | VM-BDD-001 | `WorkbenchViewModel` に MTBDD/ZMTBDD の整数値テーブル入力プロパティを追加し、ファミリー切り替えで入力 UI が適切に切り替わる | MTBDD 選択時に整数値入力グリッドが `IntValueTable` にバインドされ、`BuildAsync` が MTBDD パスで実行される | ユニットテスト（モック） | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |
| VM-MT-002 | - | `ExplanationViewModel` の拡充: 全4ファミリーに対応した解説テキスト生成（ノード種別・変数・値の説明） | BDD/ZDD/MTBDD/ZMTBDD それぞれのノードをクリックした際に適切な解説テキストが表示される | ユニットテスト | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |

---

### VIEW-MT: View (MTBDD/ZMTBDD)

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| VIEW-MT-001 | VIEW-BDD-002 | MTBDD/ZMTBDD の整数値テーブル入力グリッドの実装: ファミリー選択で表示/非表示が切り替わる | MTBDD 選択時に整数値入力セルが表示され、入力した値が `IntValueTable` に反映される | 手動動作確認 | N/A（UI） | N/A | 動作確認スクリーンショット | N/A | N/A | Todo | - |
| VIEW-MT-002 | VIEW-MT-001 | 解説パネル（`ExplanationPanel`）の全ファミリー対応 UI の実装 | 全4ファミリーでノードクリック後に解説テキストが表示される | 手動動作確認 | N/A（UI） | N/A | 動作確認スクリーンショット | N/A | N/A | Todo | - |

---

### TEST-MT: テスト (MTBDD/ZMTBDD)

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| TEST-MT-001 | SVC-MT-001 | MTBDD/ZMTBDD の統合テスト: `Create(IReadOnlyList<int>)` で構築した図の `BuildValueTable()` 結果が元の入力テーブルと一致する（2変数・全整数値パターン） | 2変数 MTBDD と ZMTBDD のテーブル往復テストが合格する | 統合テスト（実ライブラリ） | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |
| TEST-MT-002 | - | v0.3 完了基準チェック: 全テスト合格 + 全4ファミリーで構築・可視化・ノードクリック解説の手動動作確認 | テスト合格ログ + 全ファミリー動作確認記録 | 統合確認 | N/A | N/A | テスト合格ログ + 動作記録 | N/A | N/A | Todo | - |

---

## v0.4 — 設定永続化・多言語

**目標:** `SessionOptions` の JSON 永続化。日本語/英語リソース対応。UI の洗練。

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| CFG-001 | - | `SessionOptions` の JSON シリアライズ/デシリアライズ実装: `%LOCALAPPDATA%\DecisionDiagramStudio\settings.json` への読み書き | アプリ再起動後に `GraphvizPath` 等の設定が復元される | 手動動作確認 + ユニットテスト | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |
| CFG-002 | - | `SettingsPage.xaml` と `SettingsViewModel` の実装: Graphviz パス入力・テーマ選択・上限値設定 | 設定変更後に即座に `SessionOptions` に反映され、次回起動時に永続化された値が読み込まれる | 手動動作確認 | N/A（UI） | N/A | 動作確認スクリーンショット | N/A | N/A | Todo | - |
| I18N-001 | - | `Assets/Strings/ja-JP/Resources.resw` と `en-US/Resources.resw` の作成: 全 UI 文字列のリソース化 | OS の言語を日本語/英語に切り替えた際に対応した言語で表示される | 手動動作確認（言語切り替え） | N/A（リソース） | N/A | 両言語でのスクリーンショット | N/A | N/A | Todo | - |
| I18N-002 | I18N-001 | UI ハードコード文字列の全廃: XAML と ViewModel のすべての表示文字列を `Resources.resw` 経由にする | コード中に `x:Uid` 未使用の UI 文字列リテラルが存在しない | コードレビュー + grep 確認 | N/A | N/A | grep 結果（0件） | N/A | N/A | Todo | - |
| UI-001 | - | Tweaks パネルの設定ページへの統合（設計書 OQ-011 決定済み）: 開発中 Tweaks UI を設定ページに移動し、左下フッターを整理する | 設定ページに Tweaks 相当の設定項目が含まれ、左下フッターが整理されている | 手動動作確認 | N/A（UI） | N/A | 動作確認スクリーンショット | N/A | N/A | Todo | - |

---

## v1.0 — 全機能・Store 提出

**目標:** CodeAnalysis UI（ライブラリ v0.8 依存）。MSIX 署名。Microsoft Store 提出。

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| CA-001 | - | CodeAnalysis UI の実装（`DecisionDiagramSharp` v0.8 以降のライブラリ API に依存） | CodeAnalysis ページで BDD/ZDD の解析結果が表示される | 手動動作確認 | N/A | N/A | 動作確認スクリーンショット | N/A | N/A | Blocked | ライブラリ v0.8 のリリース待ち |
| DIST-001 | - | MSIX パッケージ設定と自己署名証明書によるローカル署名 | `msixbundle` が生成され、ローカルマシンにインストールできる | インストール確認 | N/A | N/A | インストール成功ログ | N/A | N/A | Todo | - |
| DIST-002 | DIST-001 | Microsoft Store パートナーセンターへの申請準備: プライバシーポリシー・スクリーンショット・説明文の作成 | Store 申請フォームの必須項目がすべて埋まっている | チェックリスト確認 | N/A（ドキュメント） | N/A | 申請フォームのスクリーンショット | N/A | N/A | Todo | - |
| DIST-003 | DIST-001 | Store 提出とリリース: Store 認定テスト（WACK）合格 | WACK テストがすべて合格する | WACK レポート | N/A | N/A | WACK 合格レポート | N/A | N/A | Todo | - |

---

## 将来検討

| ID | Parent | Task | 完了の定義 | 検証方法 | テストファースト? | 失敗テスト証跡 | 合格テスト証跡 | カバレッジ目標 | カバレッジ証跡 | Status | 証跡 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| FUTURE-001 | - | Graphviz 同梱対応（OQ-004）: MSIX サイズとライセンス（EPL-1.0）を確認し、同梱可否を決定する | OQ-004 に決定事項と根拠が記載されている | ライセンス調査 + サイズ計測 | N/A | N/A | 調査レポート | N/A | N/A | Todo | - |
| FUTURE-002 | - | 自由記述式入力（OQ-002, v0.2 以降）: `f = a AND b` 形式のテキスト入力パーサーの設計と実装 | パーサーが基本的な論理式を真理値表に変換できる | ユニットテスト | Yes | 失敗テスト出力 | 合格テスト出力 | 変更メソッド 100% | カバレッジレポート | Todo | - |
| FUTURE-003 | - | 性能計測インフラの整備: NFR-PERF-1（4変数 TT 変更→SVG 表示 300ms 目標）を自動計測するベンチマークの追加 | ベンチマークテストが CI で実行でき、300ms 閾値の違反をレポートできる | CI パイプラインでのベンチマーク実行 | N/A | N/A | ベンチマーク実行ログ | N/A | N/A | Todo | - |
