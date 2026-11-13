// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using OpenPhysical.CardEdge.Credential;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Domain.Twic;
using OpenPhysical.Middleware.Infrastructure;
using OpenPhysical.Middleware.Inspection;
using OpenPhysical.Middleware.Piv.Codec;
using OpenPhysical.Testing.Piv;

namespace OpenPhysical.Middleware.Tests;

internal sealed class TwicReferenceSession : ILocalCardSession
{
    private readonly ReferenceTransport transport;

    internal TwicReferenceSession(
        TwicCardModel model,
        ImmutableArray<byte> chuid,
        ImmutableArray<byte> fingerprint,
        ImmutableArray<byte> certificate = default,
        Maybe<LiveCredentialCardFixture.ProofTransport> proof = default,
        ImmutableArray<byte> privacyKey = default,
        bool contactless = false
    )
    {
        transport = new(model, chuid, fingerprint, certificate, proof, privacyKey);
        InterfaceType = contactless ? PivInterfaceType.Contactless : PivInterfaceType.Contact;
        Card = new CardHandle(
            SessionId.ToString(),
            "twic-reference",
            ConnectionMode.Exclusive,
            transport
        );
    }

    internal bool Current { get; set; } = true;
    internal int ProofCommands => transport.ProofCommands;
    public Guid SessionId { get; } = Guid.NewGuid();
    public long Generation => Current ? 1 : 2;
    public ICard Card { get; }
    public PivInterfaceType InterfaceType { get; }

    public UnitResult<VerificationError> EnsureCurrent() =>
        Current
            ? UnitResult.Success<VerificationError>()
            : UnitResult.Failure(
                new VerificationError(VerificationErrorCodes.EvidenceUnavailable, "fixture.changed")
            );

    public void Dispose() => Current = false;

    private sealed class ReferenceTransport(
        TwicCardModel model,
        ImmutableArray<byte> chuid,
        ImmutableArray<byte> fingerprint,
        ImmutableArray<byte> certificate,
        Maybe<LiveCredentialCardFixture.ProofTransport> proof,
        ImmutableArray<byte> privacyKey
    ) : IApduTransport
    {
        private bool twicSelected;
        internal int ProofCommands { get; private set; }

        public PivApduResponse Transmit(PivApduCommand command, PivInterfaceType interfaceType) =>
            Transmit(command);

        public PivApduResponse Transmit(PivApduCommand command) =>
            command.Ins switch
            {
                0x87 => Authenticate(command),
                0xa4 => Select(command),
                _ => GetData(command),
            };

        private PivApduResponse Authenticate(PivApduCommand command)
        {
            if ((command.Cla & 0x10) == 0)
                ProofCommands++;
            twicSelected.Should().Be(model == TwicCardModel.Nexgen);
            return proof.HasValue
                ? proof.Value.Transmit(command)
                : PivApduResponse.CreateEmpty(0x6d, 0);
        }

        private PivApduResponse Select(PivApduCommand command)
        {
            twicSelected = command.Data.SequenceEqual(TwicConstants.TwicAidPartial);
            if (!twicSelected)
            {
                command.Data.Should().Equal(PivConstants.PivAid);
                return PivApduResponse.CreateEmpty(0x90, 0);
            }
            return new(
                PivTlvWriter.Write(
                    0x4f,
                    (
                        model == TwicCardModel.Legacy
                            ? TwicConstants.TwicAidLegacy
                            : TwicConstants.TwicAidNexgen
                    ).ToArray()
                ),
                0x90,
                0
            );
        }

        private PivApduResponse GetData(PivApduCommand command)
        {
            command.Ins.Should().Be(0xcb);
            var tag = PivTlvReader.ReadAll(command.Data.ToArray()).Single().Value;
            if (tag.SequenceEqual(DataObjectTag.CardAuthenticationCertificate.Bytes))
                return certificate.IsDefaultOrEmpty
                    ? PivApduResponse.CreateEmpty(0x6a, 0x82)
                    : new(PivTlvWriter.Write(0x53, certificate.ToArray()), 0x90, 0);
            twicSelected.Should().BeTrue();
            var definition = TwicDataObjectRegistry
                .GetDefinitions(model)
                .Single(value => value.Tag.Bytes.SequenceEqual(tag));
            var payload = definition.Name switch
            {
                "Cardholder Unique Identifier (CHUID)" => chuid,
                "TWIC Privacy Key (TPK) Container" => privacyKey,
                _ => fingerprint,
            };
            return new(PivTlvWriter.Write(0x53, payload.ToArray()), 0x90, 0);
        }
    }
}
