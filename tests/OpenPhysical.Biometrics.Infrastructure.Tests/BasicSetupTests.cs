using NUnit.Framework;
using FluentAssertions;

namespace OpenPhysical.Biometrics.Infrastructure.Tests;

/// <summary>
/// Very basic tests to verify NuGet packages are correctly installed
/// </summary>
[TestFixture]
public class BasicSetupTests
{
    [Test]
    public void NUnit_Framework_Is_Available()
    {
        Assert.That(1 + 1, Is.EqualTo(2));
        Assert.Pass("NUnit is working correctly");
    }

    [Test]
    public void FluentAssertions_Is_Available()
    {
        var value = 42;
        value.Should().Be(42);
        value.Should().BeGreaterThan(0);
        
        var text = "Hello, Test";
        text.Should().StartWith("Hello");
        text.Should().EndWith("Test");
        text.Should().Contain(",");
    }

    [Test]
    public void All_Required_Packages_Are_Present()
    {
        // Verify we can use types from each required package
        
        // NUnit - already verified by this test running
        
        // FluentAssertions
        var fluentAssertionsType = typeof(FluentAssertions.AssertionExtensions);
        fluentAssertionsType.Should().NotBeNull();
        
        // CSharpFunctionalExtensions
        var resultType = typeof(CSharpFunctionalExtensions.Result);
        resultType.Should().NotBeNull();
        
        var maybeType = typeof(CSharpFunctionalExtensions.Maybe<>);
        maybeType.Should().NotBeNull();
    }

    [Test]
    public void Test_Project_Configuration_Is_Correct()
    {
        // Verify target framework
        var targetFramework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        targetFramework.Should().Contain(".NET");
        
        // Log the framework for diagnostic purposes
        TestContext.WriteLine($"Running on: {targetFramework}");
        TestContext.WriteLine($"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        TestContext.WriteLine($"Process Architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
    }
}