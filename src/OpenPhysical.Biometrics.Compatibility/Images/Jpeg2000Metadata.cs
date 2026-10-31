namespace OpenPhysical.Domains.Images;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using OpenPhysical.Domains.Biometrics;

/// <summary>
/// Represents JPEG 2000-specific metadata.
/// </summary>
public sealed record Jpeg2000Metadata
{
    /// <summary>
    /// Gets the base image metadata.
    /// </summary>
    public ImageMetadata BaseMetadata { get; }

    /// <summary>
    /// Gets the number of quality layers.
    /// </summary>
    public int QualityLayers { get; }

    /// <summary>
    /// Gets the number of resolution levels.
    /// </summary>
    public int ResolutionLevels { get; }

    /// <summary>
    /// Gets the tile size if the image is tiled.
    /// </summary>
    public Maybe<(int Width, int Height)> TileSize { get; }

    /// <summary>
    /// Gets the progression order.
    /// </summary>
    public Maybe<string> ProgressionOrder { get; }

    /// <summary>
    /// Gets whether the image has regions of interest (ROI).
    /// </summary>
    public bool HasRoi { get; }

    /// <summary>
    /// Gets the code-block size.
    /// </summary>
    public Maybe<(int Width, int Height)> CodeBlockSize { get; }

    /// <summary>
    /// Gets the compression ratio if available.
    /// </summary>
    public Maybe<double> CompressionRatio { get; }

    /// <summary>
    /// Gets JP2 box information.
    /// </summary>
    public ImmutableList<Jp2BoxInfo> Boxes { get; }

    private Jpeg2000Metadata(
        ImageMetadata baseMetadata,
        int qualityLayers,
        int resolutionLevels,
        Maybe<(int Width, int Height)> tileSize,
        Maybe<string> progressionOrder,
        bool hasRoi,
        Maybe<(int Width, int Height)> codeBlockSize,
        Maybe<double> compressionRatio,
        ImmutableList<Jp2BoxInfo> boxes
    )
    {
        BaseMetadata = baseMetadata;
        QualityLayers = qualityLayers;
        ResolutionLevels = resolutionLevels;
        TileSize = tileSize;
        ProgressionOrder = progressionOrder;
        HasRoi = hasRoi;
        CodeBlockSize = codeBlockSize;
        CompressionRatio = compressionRatio;
        Boxes = boxes ?? ImmutableList<Jp2BoxInfo>.Empty;
    }

    /// <summary>
    /// Creates a new Jpeg2000Metadata instance.
    /// </summary>
    public static Result<Jpeg2000Metadata> Create(
        ImageMetadata baseMetadata,
        int qualityLayers = 1,
        int resolutionLevels = 1,
        Maybe<(int Width, int Height)> tileSize = default,
        Maybe<string> progressionOrder = default,
        bool hasRoi = false,
        Maybe<(int Width, int Height)> codeBlockSize = default,
        Maybe<double> compressionRatio = default,
        ImmutableList<Jp2BoxInfo>? boxes = null
    )
    {
        if (baseMetadata.Format != ImageFormat.Jpeg2000)
            return Result.Failure<Jpeg2000Metadata>("Base metadata must be for JPEG 2000 format");

        if (qualityLayers < 1)
            return Result.Failure<Jpeg2000Metadata>("Quality layers must be at least 1");

        if (resolutionLevels < 1)
            return Result.Failure<Jpeg2000Metadata>("Resolution levels must be at least 1");

        if (compressionRatio.HasValue && compressionRatio.Value <= 0)
            return Result.Failure<Jpeg2000Metadata>("Compression ratio must be positive");

        return Result.Success(
            new Jpeg2000Metadata(
                baseMetadata,
                qualityLayers,
                resolutionLevels,
                tileSize,
                progressionOrder,
                hasRoi,
                codeBlockSize,
                compressionRatio,
                boxes ?? ImmutableList<Jp2BoxInfo>.Empty
            )
        );
    }
}

/// <summary>
/// Represents information about a JP2 box.
/// </summary>
public sealed record Jp2BoxInfo(string Type, long Length, long Offset, Maybe<string> Description);
