using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Infrastructure.Tests;

/// <summary>
/// Very basic tests to verify NuGet packages are correctly installed
/// </summary>
[TestFixture]
internal class BasicSetupTests
{
    [Test]
    public void NUnit_Framework_Is_Available()
    {
        Assert.That(1 + 1, Is.EqualTo(2));
        Assert.Pass("NUnit is working correctly");
    }

    [Test]
    public void CSharpFunctionalExtensions_AwesomeAssertions_Is_Available()
    {
        // Test with Result<T> to verify CSharpFunctionalExtensions.AwesomeAssertions works
        var successResult = CSharpFunctionalExtensions.Result.Success(42);
        successResult.Should().SucceedWith(42);

        var failureResult = CSharpFunctionalExtensions.Result.Failure<int>("error");
        failureResult.Should().Fail().Which.Should().Be("error");
    }

    [Test]
    public void All_Required_Packages_Are_Present()
    {
        // Verify we can use types from each required package

        // NUnit - already verified by this test running

        // CSharpFunctionalExtensions.AwesomeAssertions - just verify the extensions work
        var result = CSharpFunctionalExtensions.Result.Success("test");
        result.Should().Succeed(); // This verifies the extensions are available

        // CSharpFunctionalExtensions
        var resultType = typeof(CSharpFunctionalExtensions.Result);
        Assert.That(resultType, Is.Not.Null);

        var maybeType = typeof(CSharpFunctionalExtensions.Maybe<>);
        Assert.That(maybeType, Is.Not.Null);
    }

    [Test]
    public void Test_Project_Configuration_Is_Correct()
    {
        // Verify target framework
        var targetFramework = System
            .Runtime
            .InteropServices
            .RuntimeInformation
            .FrameworkDescription;
        targetFramework.Should().Contain(".NET");

        // Log the framework for diagnostic purposes
        TestContext.WriteLine($"Running on: {targetFramework}");
        TestContext.WriteLine(
            $"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}"
        );
        TestContext.WriteLine(
            $"Process Architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}"
        );
    }
}
