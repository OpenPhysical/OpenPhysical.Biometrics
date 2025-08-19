using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Tests.Common;

/// <summary>
/// Base class for all tests providing common setup and utilities
/// </summary>
[TestFixture]
public abstract class TestBase
{
    /// <summary>
    /// Gets the test service provider used to resolve test dependencies.
    /// </summary>
    protected IServiceProvider ServiceProvider { get; private set; } = null!;

    /// <summary>
    /// Gets the logger instance for the tests.
    /// </summary>
    protected ILogger Logger { get; private set; } = null!;

    /// <summary>
    /// One-time setup for the test fixture, configuring services and logger.
    /// </summary>
    [OneTimeSetUp]
    public virtual void OneTimeSetUp()
    {
        // Configure test services
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        // Configure test logger
        Logger = Substitute.For<ILogger>();
    }

    /// <summary>
    /// One-time teardown for the test fixture, disposing services.
    /// </summary>
    [OneTimeTearDown]
    public virtual void OneTimeTearDown()
    {
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    /// Setup method executed before each test. Override for test-specific initialization.
    /// </summary>
    [SetUp]
    public virtual void SetUp()
    {
        // Override in derived classes for test-specific setup
    }

    /// <summary>
    /// Teardown method executed after each test. Override for test-specific cleanup.
    /// </summary>
    [TearDown]
    public virtual void TearDown()
    {
        // Override in derived classes for test-specific cleanup
    }

    /// <summary>
    /// Configure test services
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Override in derived classes to add specific services
    }

    /// <summary>
    /// Create a mock logger for a specific type
    /// </summary>
    protected ILogger<T> CreateMockLogger<T>()
    {
        return Substitute.For<ILogger<T>>();
    }

    /// <summary>
    /// Assert that an action throws a specific exception
    /// </summary>
    protected static void AssertThrows<TException>(Action action, string? expectedMessage = null)
        where TException : Exception
    {
        var exception = Assert.Throws<TException>(() => action());

        if (!string.IsNullOrEmpty(expectedMessage))
        {
            exception!.Message.Should().Contain(expectedMessage);
        }
    }

    /// <summary>
    /// Assert that an async action throws a specific exception
    /// </summary>
    protected static async Task AssertThrowsAsync<TException>(
        Func<Task> action,
        string? expectedMessage = null
    )
        where TException : Exception
    {
        TException? exception = null;
        try
        {
            await action();
            Assert.Fail(
                $"Expected {typeof(TException).Name} to be thrown but no exception was thrown."
            );
        }
        catch (TException ex)
        {
            exception = ex;
        }

        if (!string.IsNullOrEmpty(expectedMessage))
        {
            exception!.Message.Should().Contain(expectedMessage);
        }
    }

    /// <summary>
    /// Get a service from the test service provider
    /// </summary>
    protected T GetService<T>()
        where T : notnull => ServiceProvider.GetRequiredService<T>();

    /// <summary>
    /// Get an optional service from the test service provider
    /// </summary>
    protected T? GetOptionalService<T>() => ServiceProvider.GetService<T>();
}

/// <summary>
/// Base class for unit tests with additional unit test specific utilities
/// </summary>
public abstract class UnitTestBase : TestBase
{
    /// <summary>
    /// Create a substitute/mock for testing
    /// </summary>
    protected T CreateSubstitute<T>()
        where T : class => Substitute.For<T>();

    /// <summary>
    /// Create a substitute with specific constructor arguments
    /// </summary>
    protected T CreateSubstitute<T>(params object[] constructorArguments)
        where T : class => Substitute.For<T>(constructorArguments);
}
