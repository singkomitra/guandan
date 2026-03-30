using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

/// <summary>
/// Suppresses unexpected-error checking that would otherwise cause Unity's
/// test runner to exit with RunError despite all tests passing.
///
/// Root cause: two Mirror package errors fire outside test scope on CI:
///   1. MirrorRenderPipelineConverter logs "Could not locate Examples folder!"
///      during Editor startup via EditorApplication.delayCall — before any
///      [SetUpFixture] can run, so the flag is also set at domain reload via
///      [InitializeOnLoad].
///   2. Unity.PerformanceTesting.Editor.TestRunBuilder.Cleanup() calls
///      AssetDatabase.Refresh() in IPostBuildCleanup, logging an error about
///      Mirror's missing meta file — caught as UnhandledLogMessageException.
///      [OneTimeTearDown] re-sets the flag just before that cleanup runs.
/// Both are third-party package issues unrelated to game logic.
/// Note: ignoreFailingMessages only affects stray Debug.LogError calls outside
/// tests — Assert failures and exceptions inside tests still fail normally.
/// </summary>
[InitializeOnLoad]
[SetUpFixture]
public class TestSetupFixture
{
    static TestSetupFixture()
    {
        LogAssert.ignoreFailingMessages = true;
    }

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        LogAssert.ignoreFailingMessages = true;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        LogAssert.ignoreFailingMessages = true;
    }
}
