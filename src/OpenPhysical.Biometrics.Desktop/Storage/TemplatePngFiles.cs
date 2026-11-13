// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;

internal static class TemplatePngFiles
{
    internal static Result<ImmutableArray<TemplatePngDescriptor>, DesktopError> AddAll(
        ImmutableArray<TemplatePackageFile>.Builder files,
        int width,
        int height,
        ImmutableArray<(string Name, ImmutableArray<byte> Pixels)> planes
    )
    {
        var result = ImmutableArray.CreateBuilder<TemplatePngDescriptor>();
        foreach (var plane in planes)
        {
            var added = Add(files, plane.Name, plane.Pixels, width, height);
            if (added.IsFailure)
                return added.Error;
            result.Add(added.Value);
        }
        return result.ToImmutable();
    }

    internal static async Task<
        Result<ImmutableArray<ImmutableArray<byte>>, DesktopError>
    > ReadAllAsync(
        TemplateCompanionSource source,
        ImmutableArray<TemplatePngDescriptor> descriptors,
        int width,
        int height,
        CancellationToken token
    )
    {
        var result = ImmutableArray.CreateBuilder<ImmutableArray<byte>>();
        foreach (var descriptor in descriptors)
        {
            var read = await ReadAsync(source, descriptor, width, height, token)
                .ConfigureAwait(false);
            if (read.IsFailure)
                return read.Error;
            result.Add(read.Value);
        }
        return result.ToImmutable();
    }

    internal static Result<TemplatePngDescriptor, DesktopError> Add(
        ImmutableArray<TemplatePackageFile>.Builder files,
        string name,
        ImmutableArray<byte> pixels,
        int width,
        int height
    ) =>
        GrayPngCodec
            .Encode(pixels, width, height)
            .Map(bytes =>
            {
                files.Add(new TemplatePackageFile(name, bytes));
                return new TemplatePngDescriptor(
                    name,
                    TemplateSha256.Of(bytes),
                    TemplateSha256.Of(pixels.ToArray())
                );
            });

    internal static async Task<Result<ImmutableArray<byte>, DesktopError>> ReadAsync(
        TemplateCompanionSource source,
        TemplatePngDescriptor descriptor,
        int width,
        int height,
        CancellationToken cancellationToken
    )
    {
        if (!Valid(descriptor))
            return Invalid("A processing companion name or hash is invalid.");
        var input = await source
            .ReadAsync(descriptor.FileName, 32 * 1024 * 1024, cancellationToken)
            .ConfigureAwait(false);
        if (input.IsFailure)
            return input.Error;
        try
        {
            if (!Same(TemplateSha256.Of(input.Value), descriptor.Sha256))
                return Invalid("A required PNG companion hash does not match.");
            return GrayPngCodec
                .Decode(input.Value, width, height)
                .Ensure(
                    pixels => Same(TemplateSha256.Of(pixels.ToArray()), descriptor.PixelSha256),
                    Invalid("A required PNG companion pixel hash does not match.")
                );
        }
        finally
        {
            Array.Clear(input.Value);
        }
    }

    internal static bool Valid(TemplatePngDescriptor descriptor) =>
        descriptor is not null
        && SafeName(descriptor.FileName)
        && Hash(descriptor.Sha256)
        && Hash(descriptor.PixelSha256);

    internal static bool SafeName(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && !Path.IsPathRooted(name)
        && name == Path.GetFileName(name)
        && !name.Contains('\\')
        && name.EndsWith(".png", StringComparison.Ordinal);

    internal static bool Hash(string value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    internal static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    internal static DesktopError Invalid(string message) => new("Storage.Manifest", message);
}
