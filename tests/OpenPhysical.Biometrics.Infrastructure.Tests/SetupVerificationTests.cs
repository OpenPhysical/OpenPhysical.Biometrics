using NUnit.Framework;
using CSharpFunctionalExtensions;
using FluentAssertions;

namespace OpenPhysical.Biometrics.Infrastructure.Tests;

/// <summary>
/// Basic tests to verify the test infrastructure is set up correctly
/// </summary>
[TestFixture]
public class SetupVerificationTests
{
    [Test]
    public void NUnit_Should_Be_Working()
    {
        // Basic NUnit assertion
        Assert.That(1 + 1, Is.EqualTo(2));
    }

    [Test]
    public void CSharpFunctionalExtensions_Should_Be_Available()
    {
        // Test Result<T> type
        var successResult = Result.Success<int, string>(42);
        var failureResult = Result.Failure<int, string>("Error message");
        
        Assert.That(successResult.IsSuccess, Is.True);
        Assert.That(failureResult.IsFailure, Is.True);
        Assert.That(successResult.Value, Is.EqualTo(42));
        Assert.That(failureResult.Error, Is.EqualTo("Error message"));
    }

    [Test]
    public void FluentAssertions_Should_Be_Working()
    {
        // Test FluentAssertions
        var testString = "Hello, World!";
        var testNumber = 42;
        var testList = new[] { 1, 2, 3, 4, 5 };
        
        testString.Should().NotBeNullOrEmpty();
        testString.Should().StartWith("Hello");
        testString.Should().EndWith("World!");
        
        testNumber.Should().Be(42);
        testNumber.Should().BeGreaterThan(0);
        
        testList.Should().HaveCount(5);
        testList.Should().Contain(3);
        testList.Should().BeInAscendingOrder();
    }

    [Test]
    public void Maybe_Type_Should_Be_Available()
    {
        // Test Maybe<T> type
        var someValue = Maybe<int>.From(42);
        var noneValue = Maybe<int>.None;
        
        Assert.That(someValue.HasValue, Is.True);
        Assert.That(noneValue.HasNoValue, Is.True);
        Assert.That(someValue.Value, Is.EqualTo(42));
    }

    [Test]
    public void Result_And_Maybe_Should_Work_Together()
    {
        // Test combining Result and Maybe
        var result = GetOptionalValue(true);
        
        result.IsSuccess.Should().BeTrue();
        result.Value.HasValue.Should().BeTrue();
        result.Value.Value.Should().Be("Success!");
        
        var emptyResult = GetOptionalValue(false);
        emptyResult.IsSuccess.Should().BeTrue();
        emptyResult.Value.HasNoValue.Should().BeTrue();
    }
    
    private static Result<Maybe<string>, string> GetOptionalValue(bool shouldHaveValue)
    {
        if (shouldHaveValue)
        {
            return Result.Success<Maybe<string>, string>(Maybe<string>.From("Success!"));
        }
        
        return Result.Success<Maybe<string>, string>(Maybe<string>.None);
    }

    [Test]
    public void Test_Infrastructure_References_Are_Valid()
    {
        // This test verifies that we can reference the Infrastructure project
        var infrastructureAssembly = typeof(OpenPhysical.Biometrics.Infrastructure.Services.FacialImageEncoderService).Assembly;
        infrastructureAssembly.Should().NotBeNull();
        
        // Verify Core project reference
        var coreAssembly = typeof(OpenPhysical.Biometrics.Core.Domain.Transformations.ProcessingOptions).Assembly;
        coreAssembly.Should().NotBeNull();
    }
}