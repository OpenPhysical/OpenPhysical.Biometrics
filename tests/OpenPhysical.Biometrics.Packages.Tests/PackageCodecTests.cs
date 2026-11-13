// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages.Tests;

using System.Collections.Immutable;
using System.Text;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class PackageCodecTests
{
    [Test]
    public void SealKeepsIdentityAndArtifactsPrivateAndOpenRestoresTheirExactBytes()
    {
        using var secret = PackageSecret.Create("public test passphrase"u8).Value;
        var package = Example();
        var first = BiometricPackageCodec.Seal(package, secret, PackageProtectionPolicy.Default);
        var second = BiometricPackageCodec.Seal(package, secret, PackageProtectionPolicy.Default);
        first.Should().Succeed();
        second.Should().Succeed();
        first.Value.Should().NotEqual(second.Value);
        Encoding.UTF8.GetString(first.Value).Should().NotContain("Example Person");
        var opened = BiometricPackageCodec.Open(
            first.Value,
            secret,
            PackageProtectionPolicy.Default
        );
        opened.Should().Succeed();
        opened.Value.PersonId.Should().Be(package.PersonId);
        opened.Value.Label.Should().Be(package.Label);
        opened.Value.ExternalId.HasValue.Should().BeTrue();
        opened.Value.ExternalId.Value.Should().Be(package.ExternalId.Value);
        opened.Value.EvidenceJson.Should().Equal(package.EvidenceJson);
        opened.Value.Artifacts.Should().HaveCount(1);
        opened.Value.Artifacts[0].Bytes.Should().Equal(package.Artifacts[0].Bytes);
    }

    [Test]
    public void WrongPassphraseAndAnyAuthenticatedHeaderCiphertextOrTagChangeExposeNoPackage()
    {
        using var secret = PackageSecret.Create("public test passphrase"u8).Value;
        using var wrong = PackageSecret.Create("a different public passphrase"u8).Value;
        var sealedBytes = BiometricPackageCodec
            .Seal(Example(), secret, PackageProtectionPolicy.Default)
            .Value;
        BiometricPackageCodec
            .Open(sealedBytes, wrong, PackageProtectionPolicy.Default)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PackageErrorCodes.AuthenticationFailed);
        foreach (var position in new[] { 36, 52, 64, sealedBytes.Length - 1 })
        {
            var tampered = sealedBytes.ToArray();
            tampered[position] ^= 1;
            BiometricPackageCodec
                .Open(tampered, secret, PackageProtectionPolicy.Default)
                .Should()
                .Fail()
                .Which.Code.Should()
                .Be(PackageErrorCodes.AuthenticationFailed);
        }
    }

    [Test]
    public void InvalidNamesDuplicatesAndInvalidUtf8AreRejectedBeforeEncoding()
    {
        PackageArtifact
            .Create("../face.bin", [1], "face", "incits385")
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PackageErrorCodes.InvalidArtifact);
        var a = PackageArtifact.Create("face.bin", [1], "face", "incits385").Value;
        var b = PackageArtifact.Create("FACE.bin", [2], "face", "incits385").Value;
        BiometricPackage
            .Create("example", "Example", Maybe<string>.None, "{}"u8, [a, b])
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PackageErrorCodes.InvalidArtifact);
        PackageSecret
            .Create([255, 254])
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PackageErrorCodes.InvalidSecret);
        PackageSecret.Create("fourteen chars"u8).Should().Fail();
    }

    [Test]
    public void DisposedSecretAndBoundedPolicyCannotBeUsedToProduceAnUnprotectedFile()
    {
        var secret = PackageSecret.Create("public test passphrase"u8).Value;
        secret.Dispose();
        BiometricPackageCodec
            .Seal(Example(), secret, PackageProtectionPolicy.Default)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PackageErrorCodes.SecretDisposed);
        PackageProtectionPolicy.Create(128, 256, 64).Should().Fail();
        using var valid = PackageSecret.Create("public test passphrase"u8).Value;
        var tiny = PackageProtectionPolicy.Create(128, 64, 2).Value;
        BiometricPackageCodec
            .Seal(Example(), valid, tiny)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be(PackageErrorCodes.LimitExceeded);
    }

    [Test]
    public async Task CodecCompletesWhenCalledFromASingleConcurrencyScheduler()
    {
        using var secret = PackageSecret.Create("public test passphrase"u8).Value;
        var scheduler = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, 1);
        var sealedPackage = Task.Factory.StartNew(
            () => BiometricPackageCodec.Seal(Example(), secret, PackageProtectionPolicy.Default),
            CancellationToken.None,
            TaskCreationOptions.None,
            scheduler.ExclusiveScheduler
        );
        var result = await sealedPackage.WaitAsync(TimeSpan.FromSeconds(10));
        result.Should().Succeed();
        scheduler.Complete();
        await scheduler.Completion.WaitAsync(TimeSpan.FromSeconds(2));
    }

    internal static BiometricPackage Example() =>
        BiometricPackage
            .Create(
                "example-person",
                "Example Person",
                Maybe.From("example-external"),
                "{\"capturePlan\":[\"face\",\"fingerprints\"]}"u8,
                [
                    PackageArtifact
                        .Create(
                            "fingerprints.fmr",
                            "public fixture bytes"u8,
                            "fingerprints",
                            "incits378-2004"
                        )
                        .Value,
                ]
            )
            .Value;
}
