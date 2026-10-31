// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Piv.Tests;

using System.Collections.Immutable;
using System.Text.Json;
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
        Assert.Multiple(() =>
        {
            Assert.That(report.Kind, Is.EqualTo(BiometricRecordKind.Cbeff));
            Assert.That(report.Structure.Diagnostics, Is.Empty);
            Assert.That(report.ProfileDiagnostics, Is.Empty);
            Assert.That(
                report.FingerViews.Select(view => view.FingerPosition),
                Is.EqualTo(new byte[] { 2, 7 })
            );
            Assert.That(
                report
                    .Structure.Fields.Single(field =>
                        field.Name == "BiometricData.FormatIdentifier"
                    )
                    .Offset,
                Is.EqualTo(88)
            );
            Assert.That(report.Structure.Fields.All(field => field.Value is null), Is.True);
            Assert.That(report.HasSignatureBlock, Is.True);
        });
    }

    [Test]
    public void SensitiveFieldValuesRequireExplicitOptInAndRoundTripAsReportJson()
    {
        var report = BiometricInspector.Inspect(
            Wrapped(),
            Now,
            new InspectionOptions(IncludeSensitiveValues: true)
        );
        Assert.That(report.Structure.Fields.Any(field => field.Value is not null), Is.True);
        var restored = JsonSerializer.Deserialize<BiometricInspectionReport>(
            JsonSerializer.Serialize(report)
        );
        Assert.That(restored!.FingerViews, Is.EqualTo(report.FingerViews));
    }

    [Test]
    public void EveryTruncatedPrefixProducesLocatedStructuralFindings()
    {
        var bytes = Wrapped();
        for (var length = 0; length < bytes.Length; length++)
        {
            var report = BiometricInspector.Inspect(bytes.AsSpan(0, length), Now);
            Assert.That(report.Structure.Diagnostics, Is.Not.Empty, $"prefix {length}");
            Assert.That(
                report.Structure.Diagnostics.All(diagnostic => diagnostic.Offset >= 0),
                Is.True
            );
        }
    }

    [Test]
    public void NestedWrappersStopAtTheBiometricRecordBoundary()
    {
        var outer = Wrap(Wrapped());
        var report = BiometricInspector.Inspect(CbeffCodec.Write(outer), Now);
        Assert.That(
            report.Structure.Diagnostics.Select(error => error.Code),
            Does.Contain("BiometricFormat")
        );
    }

    [Test]
    public void PivProfileFindingsRemainSeparateFromStructuralParsing()
    {
        var bytes = Incits378Codec.Write(Fingerprint() with { XResolution = 196 });
        var report = BiometricInspector.Inspect(bytes, Now);
        Assert.That(report.Structure.Diagnostics, Is.Empty);
        Assert.That(
            report.ProfileDiagnostics.Select(error => error.Code),
            Does.Contain("Resolution")
        );
    }

    [Test]
    public void PublicContractsExposeStandardAndProjectOwnedTypes()
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
        Assert.That(
            contracts.Any(type =>
                type.ToString().Contains("CSharpFunctionalExtensions", StringComparison.Ordinal)
            ),
            Is.False
        );
        Assert.That(
            contracts.Any(type =>
                type.ToString().Contains("BouncyCastle", StringComparison.Ordinal)
            ),
            Is.False
        );
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

    private static byte[] Wrapped() => CbeffCodec.Write(Wrap(Incits378Codec.Write(Fingerprint())));

    private static CbeffRecord Wrap(byte[] content) =>
        new(
            CbeffHeader.Create(
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
            ),
            ImmutableArray.Create(content),
            ImmutableArray.Create<byte>(1, 2, 3, 4)
        );
}
