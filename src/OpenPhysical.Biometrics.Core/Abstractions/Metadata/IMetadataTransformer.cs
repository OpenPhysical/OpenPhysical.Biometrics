namespace OpenPhysical.Biometrics.Core.Abstractions.Metadata;

using System.Collections.Generic;
using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Images;

/// <summary>
/// Represents a node in a metadata tree structure.
/// </summary>
public sealed class MetadataTreeNode
{
    /// <summary>
    /// Gets the label for this node.
    /// </summary>
    public string Label { get; }
    
    /// <summary>
    /// Gets the child nodes.
    /// </summary>
    public IReadOnlyList<MetadataTreeNode> Children { get; }
    
    /// <summary>
    /// Creates a new metadata tree node.
    /// </summary>
    public MetadataTreeNode(string label, IReadOnlyList<MetadataTreeNode>? children = null)
    {
        Label = label;
        Children = children ?? new List<MetadataTreeNode>();
    }
}

/// <summary>
/// Interface for transforming image metadata into display formats.
/// </summary>
public interface IMetadataTransformer
{
    /// <summary>
    /// Transforms image metadata into a tree structure for display.
    /// </summary>
    Result<MetadataTreeNode> TransformToTree(ImageMetadata metadata);
    
    /// <summary>
    /// Transforms EXIF data into a tree structure for display.
    /// </summary>
    Result<MetadataTreeNode> TransformExifToTree(ExifData exif);
    
    /// <summary>
    /// Transforms image metadata into a simplified key-value format.
    /// </summary>
    Result<IReadOnlyDictionary<string, string>> TransformToKeyValue(ImageMetadata metadata);
}