// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages.Tests;

using System.Buffers.Binary;
using System.Text;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class PackageIndependentVectorTests
{
    [Test]
    public void ReferenceArgon2AndAesGcmEnvelopeOpensWithoutSharingTheWriter()
    {
        using var secret = PackageSecret.Create("independent public passphrase"u8).Value;
        var result = BiometricPackageCodec.Open(
            Vector("valid"),
            secret,
            PackageProtectionPolicy.Default
        );
        result.Should().Succeed();
        result.Value.PersonId.Should().Be("public-fixture");
        result.Value.Artifacts[0].Bytes.Should().Equal("public non-biometric fixture"u8.ToArray());
        result.Value.ExternalId.HasNoValue.Should().BeTrue();
    }

    [TestCase("duplicate", PackageErrorCodes.InvalidArchive)]
    [TestCase("traversal", PackageErrorCodes.InvalidArchive)]
    [TestCase("symlink", PackageErrorCodes.InvalidArchive)]
    [TestCase("hash-mismatch", PackageErrorCodes.ArtifactMismatch)]
    [TestCase("extra", PackageErrorCodes.ArtifactMismatch)]
    public void AuthenticatedMalformedArchivesReleaseNoArtifacts(string vector, string code)
    {
        using var secret = PackageSecret.Create("independent public passphrase"u8).Value;
        BiometricPackageCodec
            .Open(Vector(vector), secret, PackageProtectionPolicy.Default)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(code);
    }

    [Test]
    public void HeaderResourceBoundsAndTruncationFailBeforeExpensiveUntrustedWork()
    {
        using var secret = PackageSecret.Create("independent public passphrase"u8).Value;
        var excessive = Vector("valid");
        BinaryPrimitives.WriteUInt32BigEndian(excessive.AsSpan(16, 4), uint.MaxValue);
        BiometricPackageCodec
            .Open(excessive, secret, PackageProtectionPolicy.Default)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PackageErrorCodes.InvalidEnvelope);
        foreach (var length in new[] { 0, 63, 79, excessive.Length - 1 })
            BiometricPackageCodec
                .Open(Vector("valid").AsSpan(0, length), secret, PackageProtectionPolicy.Default)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(PackageErrorCodes.InvalidEnvelope);
    }

    [Test]
    public void AuthenticatedCompressedMemberCannotExceedItsExpansionBudget()
    {
        using var secret = PackageSecret.Create("independent public passphrase"u8).Value;
        var bounded = PackageProtectionPolicy.Create(128 * 1024, 64 * 1024, 64).Value;
        BiometricPackageCodec
            .Open(Vector("expanded"), secret, bounded)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PackageErrorCodes.LimitExceeded);
    }

    [Test]
    public void UnderreportedCentralDirectoryCannotForceEagerUnboundedEntryMaterialization()
    {
        using var secret = PackageSecret.Create("independent public passphrase"u8).Value;
        var bounded = PackageProtectionPolicy.Create(1024 * 1024, 512 * 1024, 2).Value;
        BiometricPackageCodec
            .Open(Vector("underreported"), secret, bounded)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PackageErrorCodes.LimitExceeded);
    }

    private static byte[] Vector(string name) =>
        Convert.FromHexString(
            File.ReadAllText(
                    Path.Combine(
                        TestContext.CurrentContext.TestDirectory,
                        "Fixtures",
                        $"independent-{name}.opbio.hex"
                    )
                )
                .Trim()
        );
}
