namespace OpenPhysical.Biometrics.Core.Standards;

using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Standards;
using OpenPhysical.Domains.Biometrics;

/// <summary>
/// Registry interface for managing available document standards.
/// </summary>
/// <remarks>
/// The registry allows dynamic registration of document standards,
/// enabling a plugin architecture where new standards can be added
/// without modifying the core library.
/// </remarks>
public interface IStandardRegistry
{
    /// <summary>
    /// Registers a document standard implementation.
    /// </summary>
    /// <param name="standard">The standard implementation to register.</param>
    /// <returns>A Result indicating success or failure if a standard with the same ID already exists.</returns>
    Result Register(IDocumentStandard standard);

    /// <summary>
    /// Gets a document standard by its identifier.
    /// </summary>
    /// <param name="id">The standard identifier.</param>
    /// <returns>A Maybe containing the standard if found.</returns>
    Maybe<IDocumentStandard> GetStandard(DocumentStandardId id);

    /// <summary>
    /// Gets a document standard by its string identifier (case-insensitive).
    /// </summary>
    /// <param name="id">The standard identifier string.</param>
    /// <returns>A Maybe containing the standard if found.</returns>
    Maybe<IDocumentStandard> GetStandard(string id);

    /// <summary>
    /// Gets all registered document standards.
    /// </summary>
    /// <returns>All registered standard implementations.</returns>
    IEnumerable<IDocumentStandard> GetAllStandards();

    /// <summary>
    /// Checks if a standard is registered.
    /// </summary>
    /// <param name="id">The standard identifier.</param>
    /// <returns>True if the standard is registered.</returns>
    bool IsRegistered(DocumentStandardId id);

    /// <summary>
    /// Removes a standard from the registry.
    /// </summary>
    /// <param name="id">The standard identifier to remove.</param>
    /// <returns>True if the standard was removed, false if it wasn't found.</returns>
    bool Unregister(DocumentStandardId id);

    /// <summary>
    /// Gets standards that support a specific biometric modality.
    /// </summary>
    /// <param name="modality">The biometric modality to filter by.</param>
    /// <returns>Standards that support the specified modality.</returns>
    IEnumerable<IDocumentStandard> GetStandardsForModality(BiometricModality modality);

    /// <summary>
    /// Loads standards from a configuration file or directory.
    /// </summary>
    /// <param name="path">Path to configuration file or directory containing standard definitions.</param>
    /// <returns>A Result containing the number of standards loaded or an error.</returns>
    Task<Result<int>> LoadFromPathAsync(string path);
}

/// <summary>
/// Extension methods for standard registry functionality.
/// </summary>
public static class StandardRegistryExtensions
{
    /// <summary>
    /// Registers multiple standards at once.
    /// </summary>
    /// <param name="registry">The registry to add standards to.</param>
    /// <param name="standards">The standards to register.</param>
    /// <returns>A Result indicating success or containing any registration errors.</returns>
    public static Result RegisterMany(this IStandardRegistry registry, params IDocumentStandard[] standards)
    {
        var errors = new List<string>();
        
        foreach (var standard in standards)
        {
            var result = registry.Register(standard);
            if (result.IsFailure)
            {
                errors.Add($"{standard.Id}: {result.Error}");
            }
        }

        return errors.Any() 
            ? Result.Failure($"Failed to register standards: {string.Join("; ", errors)}")
            : Result.Success();
    }

    /// <summary>
    /// Gets a standard or throws if not found.
    /// </summary>
    /// <param name="registry">The registry to search.</param>
    /// <param name="id">The standard identifier.</param>
    /// <returns>The document standard.</returns>
    /// <exception cref="InvalidOperationException">Thrown when standard is not found.</exception>
    public static IDocumentStandard GetRequiredStandard(this IStandardRegistry registry, DocumentStandardId id)
    {
        return registry.GetStandard(id)
            .GetValueOrThrow($"Standard '{id}' not found in registry");
    }
}