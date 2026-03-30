using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Runs once before all EditMode tests in this assembly.
/// Suppresses known third-party errors that would otherwise cause Unity's
/// test runner to exit with RunError despite all tests passing.
/// </summary>
[SetUpFixture]
public class TestSetupFixture
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Mirror's MirrorRenderPipelineConverter logs this error on CI when the
        // package's Examples folder is absent. It is unrelated to our tests.
        LogAssert.Expect(LogType.Error, "Could not locate Examples folder!");
    }
}
