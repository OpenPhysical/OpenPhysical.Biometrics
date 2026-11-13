// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using OpenPhysical.CardEdge.Credential;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Inspection;
using OpenPhysical.Middleware.Piv.Codec;
using OpenPhysical.Testing.Piv;

namespace OpenPhysical.Middleware.Tests;

internal sealed class FingerprintReferenceSession : ILocalCardSession
{
    internal FingerprintReferenceSession(
        byte[] certificate,
        ImmutableArray<byte> chuid,
        ImmutableArray<byte> fingerprint,
        Maybe<LiveCredentialCardFixture.ProofTransport> proof = default,
        bool authenticated = true
    )
    {
        Card = new CardHandle(
            SessionId.ToString(),
            "reference",
            ConnectionMode.Exclusive,
            new ReferenceTransport(
                new Dictionary<int, ImmutableArray<byte>>
                {
                    [DataObjectTag.CardAuthenticationCertificate.Value] =
                        certificate.ToImmutableArray(),
                    [DataObjectTag.CardholderUniqueIdentifier.Value] = chuid,
                    [DataObjectTag.CardholderFingerprints.Value] = fingerprint,
                },
                proof
            )
        );
        ((CardHandle)Card).UpdateSecurityContext(
            new PivSecurityContext
            {
                IsAuthenticated = authenticated,
                InterfaceType = PivInterfaceType.Contact,
            }
        );
    }

    internal bool Current { get; set; } = true;
    public Guid SessionId { get; } = Guid.NewGuid();
    public long Generation => Current ? 1 : 2;
    public ICard Card { get; }
    public PivInterfaceType InterfaceType => PivInterfaceType.Contact;

    public UnitResult<VerificationError> EnsureCurrent() =>
        Current
            ? UnitResult.Success<VerificationError>()
            : UnitResult.Failure(
                new VerificationError(VerificationErrorCodes.EvidenceUnavailable, "fixture.changed")
            );

    public void Dispose() => Current = false;

    private sealed class ReferenceTransport(
        Dictionary<int, ImmutableArray<byte>> objects,
        Maybe<LiveCredentialCardFixture.ProofTransport> proof
    ) : IApduTransport
    {
        public PivApduResponse Transmit(PivApduCommand command, PivInterfaceType interfaceType) =>
            Transmit(command);

        public PivApduResponse Transmit(PivApduCommand command)
        {
            if (command.Ins != 0xcb && proof.HasValue)
                return proof.Value.Transmit(command);
            command.Ins.Should().Be(0xcb);
            var tag = DataObjectTag.FromBytes(
                PivTlvReader.ReadAll(command.Data.ToArray()).Single().Value.ToArray()
            );
            return objects.TryGetValue(tag.Value, out var value)
                ? new PivApduResponse(PivTlvWriter.Write(0x53, value.ToArray()), 0x90, 0)
                : PivApduResponse.CreateEmpty(0x6a, 0x82);
        }
    }
}
