using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace OpenPhysical.Biometrics.Tests.Common;

/// <summary>
/// Base class for integration tests that need biometric services
/// </summary>
public abstract class IntegrationTestBase : TestBase
{
    /// <summary>
    /// Gets the temporary directory for integration tests
    /// </summary>
    protected string TempDirectory { get; private set; } = string.Empty;

    /// <inheritdoc/>
    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        // Derived test classes in Infrastructure.Tests will add biometric services
        // This base class only provides the basic integration test infrastructure
    }

    /// <inheritdoc/>
    [SetUp]
    public override void SetUp()
    {
        base.SetUp();

        // Create temp directory for each test
        TempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"Biometrics_IntegrationTest_{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(TempDirectory);
    }

    /// <inheritdoc/>
    [TearDown]
    public override void TearDown()
    {
        base.TearDown();

        // Clean up temp directory
        if (!Directory.Exists(TempDirectory))
        {
            return;
        }
        try
        {
            Directory.Delete(TempDirectory, recursive: true);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to delete temp directory {TempDirectory}", TempDirectory);
        }
    }

    /// <summary>
    /// Create a temporary file with specified content
    /// </summary>
    protected string CreateTempFile(string fileName, byte[] content)
    {
        var filePath = Path.Combine(TempDirectory, fileName);
        File.WriteAllBytes(filePath, content);
        return filePath;
    }

    /// <summary>
    /// Create a temporary file with specified text content
    /// </summary>
    protected string CreateTempFile(string fileName, string content)
    {
        var filePath = Path.Combine(TempDirectory, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    /// <summary>
    /// Assert that a file exists and optionally check its content
    /// </summary>
    protected void AssertFileExists(string filePath, Func<byte[], bool>? contentValidator = null)
    {
        File.Exists(filePath).Should().BeTrue($"Expected file to exist: {filePath}");

        if (contentValidator != null)
        {
            var content = File.ReadAllBytes(filePath);
            contentValidator(content).Should().BeTrue("File content validation failed");
        }
    }

    /// <summary>
    /// Create a simple test image data (placeholder)
    /// </summary>
    protected byte[] CreateTestImage(int width = 640, int height = 480)
    {
        // Create a simple JPEG header
        // This is a placeholder - real tests should provide actual image data
        return new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
    }
}
