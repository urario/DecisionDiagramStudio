using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DecisionDiagramStudio.Tests;

/// <summary>
/// 概要: テストプロジェクトが動作することを確認するプレースホルダーテスト。
/// </summary>
/// <remarks>
/// 狙い: SETUP-002 の完了条件「dotnet test がゼロエラーで完了する」を満たすための最小限のテスト。
/// 実際のテスト追加後に削除してよい。
/// </remarks>
[TestClass]
public class PlaceholderTest
{
    /// <summary>
    /// 概要: テストプロジェクトのビルドと実行が正常に動作することを確認する。
    /// </summary>
    /// <remarks>
    /// 狙い: MSTest のアダプターが正しく登録されていることを保証する。
    /// </remarks>
    [TestMethod]
    public void TestProject_ShouldBuildAndRun()
    {
        // Arrange / Act / Assert — プレースホルダー: 常に合格する
        Assert.IsTrue(true);
    }
}
