// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Fingerprints;

internal sealed class ScriptedCredentialCard(params byte[][] responses) : ICredentialCard
{
    private readonly Queue<byte[]> responses = new(responses);
    internal List<byte[]> Commands { get; } = [];

    public Result<byte[], DesktopError> Transmit(byte[] command, CancellationToken token)
    {
        Commands.Add(command.ToArray());
        return responses.TryDequeue(out var response)
            ? response
            : new DesktopError("Test.Command", "An unexpected card command was sent.");
    }

    internal static byte[] Fingerprints() =>
        Fingerprints(
            UnsignedTemplateService
                .Create([StorageFixtures.Capture()], new TemplateMetadata(1, 0, 0))
                .Value
        );

    internal static byte[] Fingerprints(FingerprintRecord record)
    {
        var fmr = Incits378Codec.Write(record).Value;
        var at = DateTimeOffset.UnixEpoch;
        var header = CbeffHeader
            .Create(
                (uint)fmr.Length,
                0,
                0x1B,
                0x0201,
                at,
                at,
                at.AddYears(1),
                8,
                0x80,
                80,
                new byte[18],
                new byte[25]
            )
            .Value;
        var cbeff = CbeffCodec.Write(new CbeffRecord(header, ImmutableArray.Create(fmr), [])).Value;
        return Wrap(0x53, [.. Wrap(0xBC, cbeff), 0xFE, 0]);
    }

    private static byte[] Wrap(byte tag, byte[] data) =>
        [tag, 0x82, (byte)(data.Length >> 8), (byte)data.Length, .. data];
}
