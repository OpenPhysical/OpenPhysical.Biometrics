namespace OpenPhysical.Biometrics.Core.Domain;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

/// <summary>
/// Immutable representation of biometric data for display purposes.
/// Uses a tree structure to represent hierarchical biometric information.
/// </summary>
public sealed record BiometricDisplay
{
    /// <summary>
    /// Gets the label for this display node.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the optional value associated with this node.
    /// </summary>
    public Maybe<object> Value { get; }

    /// <summary>
    /// Gets the child nodes for hierarchical data.
    /// </summary>
    public ImmutableList<BiometricDisplay> Children { get; }

    /// <summary>
    /// Initializes a new instance of the BiometricDisplay record.
    /// </summary>
    /// <param name="label">The display label.</param>
    /// <param name="value">Optional value to display.</param>
    /// <param name="children">Child nodes for hierarchical data.</param>
    public BiometricDisplay(
        string label,
        Maybe<object> value,
        ImmutableList<BiometricDisplay> children
    )
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
        Value = value;
        Children = children ?? ImmutableList<BiometricDisplay>.Empty;
    }

    /// <summary>
    /// Creates a leaf node with a value.
    /// </summary>
    /// <param name="label">The display label.</param>
    /// <param name="value">The value to display.</param>
    /// <returns>A new BiometricDisplay instance.</returns>
    public static BiometricDisplay Leaf(string label, object value) =>
        new(label, Maybe<object>.From(value), ImmutableList<BiometricDisplay>.Empty);

    /// <summary>
    /// Creates a branch node with children but no value.
    /// </summary>
    /// <param name="label">The display label.</param>
    /// <param name="children">The child nodes.</param>
    /// <returns>A new BiometricDisplay instance.</returns>
    public static BiometricDisplay Branch(string label, ImmutableList<BiometricDisplay> children) =>
        new(label, Maybe<object>.None, children);

    /// <summary>
    /// Creates a branch node with children from params array.
    /// </summary>
    /// <param name="label">The display label.</param>
    /// <param name="children">The child nodes.</param>
    /// <returns>A new BiometricDisplay instance.</returns>
    public static BiometricDisplay Branch(string label, params BiometricDisplay[] children) =>
        new(label, Maybe<object>.None, children.ToImmutableList());
}
