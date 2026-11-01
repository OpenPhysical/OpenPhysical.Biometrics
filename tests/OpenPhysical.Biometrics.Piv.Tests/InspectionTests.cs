// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Piv.Tests;

using System.Collections.Immutable;
using System.Text.Json;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using NUnit.Framework;
using OpenPhysical.Biometrics.Formats;
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Formats.Fingerprints;

internal sealed class InspectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void WrappedInspectionUsesOriginalOffsetsAndSharesTheFingerprintCodec()
    {
        var report = BiometricInspector.Inspect(Wrapped(), Now);
        using (new AssertionScope())
        {
            report.Kind.Should().Be(BiometricRecordKind.Cbeff);
            report.Structure.Diagnostics.Should().BeEmpty();
            report.ProfileDiagnostics.Should().BeEmpty();
            report
                .FingerViews.Select(view => view.FingerPosition)
                .Should()
                .Equal(new byte[] { 2, 7 });
            report
                .Structure.Fields.Single(field => field.Name == "BiometricData.FormatIdentifier")
                .Offset.Should()
                .Be(88);
            report.Structure.Fields.All(field => field.Value.HasNoValue).Should().BeTrue();
            report.HasSignatureBlock.Should().BeTrue();
        }
    }

    [Test]
    public void SensitiveFieldValuesRequireExplicitOptInAndRoundTripAsReportJson()
    {
        var report = BiometricInspector.Inspect(
            Wrapped(),
            Now,
            new InspectionOptions(IncludeSensitiveValues: true)
        );
        report.Structure.Fields.Any(field => field.Value.HasValue).Should().BeTrue();
        if (
            JsonSerializer.Deserialize<BiometricInspectionReport>(JsonSerializer.Serialize(report))
            is not { } restored
        )
        {
            Assert.Fail("The serialized report did not restore.");
            return;
        }
        using (new AssertionScope())
        {
            restored.FingerViews.Should().Equal(report.FingerViews);
            restored.Structure.Fields.Should().Equal(report.Structure.Fields);
            restored.Structure.Fields.All(field => field.Value.HasValue).Should().BeTrue();
        }
    }

    [Test]
    public void EveryTruncatedPrefixProducesLocatedStructuralFindings()
    {
        var bytes = Wrapped();
        for (var length = 0; length < bytes.Length; length++)
        {
            var report = BiometricInspector.Inspect(bytes.AsSpan(0, length), Now);
            report.Structure.Diagnostics.Should().NotBeEmpty($"prefix {length}");
            report
                .Structure.Diagnostics.All(diagnostic => diagnostic.Offset >= 0)
                .Should()
                .BeTrue();
        }
    }

    [Test]
    public void NestedWrappersStopAtTheBiometricRecordBoundary()
    {
        var outer = Wrap(Wrapped());
        var report = BiometricInspector.Inspect(CbeffCodec.Write(outer).Value, Now);
        report
            .Structure.Diagnostics.Select(error => error.Code)
            .Should()
            .Contain("BiometricFormat");
    }

    [Test]
    public void PivProfileFindingsRemainSeparateFromStructuralParsing()
    {
        var bytes = Incits378Codec.Write(Fingerprint() with { XResolution = 196 }).Value;
        var report = BiometricInspector.Inspect(bytes, Now);
        report.Structure.Diagnostics.Should().BeEmpty();
        report.ProfileDiagnostics.Select(error => error.Code).Should().Contain("Resolution");
    }

    [Test]
    public void PublicContractsExposeResultsAndKeepCryptographyOutOfTheBoundary()
    {
        var assemblies = new[]
        {
            typeof(BiometricInspector).Assembly,
            typeof(Incits378Codec).Assembly,
        };
        var types = assemblies.SelectMany(assembly => assembly.GetExportedTypes());
        var contracts = types
            .SelectMany(type => type.GetMethods())
            .SelectMany(method =>
                method
                    .GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .Append(method.ReturnType)
            );
        contracts
            .Any(type =>
                type.ToString().Contains("CSharpFunctionalExtensions", StringComparison.Ordinal)
            )
            .Should()
            .BeTrue();
        contracts
            .Any(type => type.ToString().Contains("BouncyCastle", StringComparison.Ordinal))
            .Should()
            .BeFalse();
    }

    private static FingerprintRecord Fingerprint() =>
        new(
            1,
            1,
            0x8001,
            512,
            512,
            197,
            197,
            [
                new(2, 0, 0, 80, [new(1, 30, 40, 90, 80)], []),
                new(7, 0, 0, 80, [new(2, 32, 43, 100, 70)], []),
            ]
        );

    private static byte[] Wrapped() =>
        CbeffCodec.Write(Wrap(Incits378Codec.Write(Fingerprint()).Value)).Value;

    private static CbeffRecord Wrap(byte[] content) =>
        new(
            CbeffHeader
                .Create(
                    (uint)content.Length,
                    4,
                    0x001B,
                    0x0201,
                    Now,
                    Now,
                    Now.AddYears(1),
                    8,
                    0x80,
                    80,
                    new byte[18],
                    new byte[25],
                    securityOptions: 0x0D
                )
                .Value,
            ImmutableArray.Create(content),
            ImmutableArray.Create<byte>(1, 2, 3, 4)
        );
}
