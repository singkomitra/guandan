using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>
/// Runs once before all EditMode tests in this assembly.
/// Suppresses unexpected-error checking that would otherwise cause Unity's
/// test runner to exit with RunError despite all tests passing.
///
/// Root cause: two Mirror package errors fire outside test scope on CI:
///   1. MirrorRenderPipelineConverter logs "Could not locate Examples folder!"
///      during Editor startup (before any test runs).
///   2. Unity.PerformanceTesting.Editor.TestRunBuilder.Cleanup() calls
///      AssetDatabase.Refresh(), which logs an error about Mirror's missing
///      meta file in an immutable folder — caught as UnhandledLogMessageException.
/// Both are third-party package issues unrelated to game logic.
/// ignoreFailingMessages = true suppresses the RunError for both.
/// </summary>
[SetUpFixture]
public class TestSetupFixture
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        LogAssert.ignoreFailingMessages = true;
    }
}
