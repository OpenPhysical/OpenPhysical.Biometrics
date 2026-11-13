// SPDX-License-Identifier: GPL-2.0-or-later
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OpenPhysical.Biometrics.Desktop;
using OpenPhysical.Middleware.Cli.Commands;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class CaptureVerificationCommandTests
{
    private const string Authority = "unopened-authority.json";
    private const string Database = "unopened-cache.db";
    private const string Snapshot = "unopened-snapshot.json";

    [TestCase(true, null, false)]
    [TestCase(true, (byte)2, true)]
    [TestCase(false, (byte)0, false)]
    [TestCase(false, (byte)11, false)]
    public void FingerprintCaptureReadyRequiresAnExplicitValidFinger(
        bool ready,
        byte? finger,
        bool valid
    )
    {
        var settings = new FingerprintVerifySettings
        {
            AuthorityPath = "authority",
            DatabasePath = "cache",
            SnapshotPath = "snapshot",
            Reader = "fixture",
            CaptureDevice = "fixture",
            ContentSignerPaths = new[] { "leaf.der", "root.der" },
            CaptureReady = ready,
            Finger = finger,
        };
        Assert.That(settings.Validate().Successful, Is.EqualTo(valid));
    }

    [Test]
    public async Task NoninteractiveFingerprintCommandRefusesImplicitPromptBeforeOpeningHardware()
    {
        var result = await Run(
            new[]
            {
                "--json",
                "fingerprint",
                "verify",
                "--authority",
                Authority,
                "--database",
                Database,
                "--snapshot",
                Snapshot,
                "--reader",
                "fixture",
                "--capture-device",
                "fixture",
                "--content-signer",
                "leaf.der",
                "--content-signer",
                "root.der",
            }
        );
        Assert.That(result.Code, Is.EqualTo(2));
        Assert.That(result.Output, Does.Not.Contain("Enter card PIN"));
        Assert.That(result.Output, Does.Not.Contain("Place your"));
    }

    [TestCase("", 1, "contact", "", false)]
    [TestCase("twic.fixture", 0, "contact", "", false)]
    [TestCase("twic.fixture", 4, "contact", "", false)]
    [TestCase("twic.fixture", 1, "contact", "", true)]
    [TestCase("twic.fixture", 3, "contactless", "", false)]
    [TestCase("twic.fixture", 3, "contactless", "privacy-key.bin", true)]
    public void TwicRequiresSignedProfileMarsecAndExplicitContactlessKey(
        string profile,
        int marsec,
        string cardInterface,
        string key,
        bool valid
    )
    {
        var settings = new TwicVerifySettings
        {
            AuthorityPath = "authority",
            DatabasePath = "cache",
            SnapshotPath = "snapshot",
            Reader = "fixture",
            CaptureDevice = "fixture",
            Profile = profile,
            MarsecLevel = marsec,
            Interface = cardInterface,
            PrivacyKeyPath = key,
        };
        Assert.That(settings.Validate().Successful, Is.EqualTo(valid));
    }

    [Test]
    public async Task NoninteractiveTwicRefusesImplicitCaptureBeforeOpeningHardware()
    {
        var result = await Run(
            new[]
            {
                "--json",
                "twic",
                "verify",
                "--authority",
                Authority,
                "--database",
                Database,
                "--snapshot",
                Snapshot,
                "--reader",
                "fixture",
                "--capture-device",
                "fixture",
                "--profile",
                "twic.fixture",
                "--marsec",
                "1",
            }
        );
        Assert.That(result.Code, Is.EqualTo(2));
        Assert.That(result.Output, Does.Not.Contain("PIN"));
        Assert.That(result.Output, Does.Not.Contain("Place your"));
    }

    [Test]
    public async Task TwicHelpContainsItsPolicyOptionsWithoutSubjectPinOrContentSignerOptions()
    {
        var result = await Run(new[] { "twic", "verify", "--help" });
        Assert.That(result.Code, Is.Zero);
        Assert.That(result.Output, Does.Contain("--marsec"));
        Assert.That(result.Output, Does.Contain("--privacy-key"));
        Assert.That(result.Output, Does.Not.Contain("--pin-stdin"));
        Assert.That(result.Output, Does.Not.Contain("--content-signer"));
    }

    private static async Task<(int Code, string Output)> Run(string[] arguments)
    {
        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { Args = [], DisableDefaults = true }
        );
        builder.Logging.ClearProviders();
        using var output = new StringWriter();
        var code = await StudioVerificationApplication.RunHostedAsync(arguments, builder, output);
        return (code, output.ToString());
    }
}
