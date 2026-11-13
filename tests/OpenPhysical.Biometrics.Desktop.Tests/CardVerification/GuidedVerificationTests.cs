// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Infrastructure.Trust;
using OpenPhysical.Middleware.Tests;
using OpenPhysical.Workstation.Application.Verification;

namespace OpenPhysical.Workstation.Tests;

[TestFixture]
internal sealed class GuidedVerificationTests
{
    [Test]
    public async Task OnlyAuthenticatedChoicesCanReleaseCaptureAndSecretsAreCleared()
    {
        var bundle = await Bundle();
        var runner = new GuidedVerificationTestRunner();
        await using var coordinator = new GuidedVerificationCoordinator(
            runner,
            TimeProvider.System
        );
        var started = coordinator.Start(Request(), bundle, new byte[] { 1, 2, 3, 4, 5, 6 }, []);
        Assert.That(
            started.IsSuccess,
            Is.True,
            started.IsFailure ? started.Error.Code.ToString() : string.Empty
        );
        await runner.Ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var status = coordinator.Read(started.Value).Value;
        Assert.That(status.Phase, Is.EqualTo(GuidedVerificationPhase.AwaitingFinger));
        Assert.That(
            status.Choices,
            Is.EqualTo(ImmutableArray.Create(new GuidedFingerChoice(2, 0)))
        );
        Assert.That(coordinator.Select(started.Value, new(7, 0)).IsFailure, Is.True);
        Assert.That(runner.Captured, Is.False);
        Assert.That(coordinator.Select(started.Value, new(2, 0)).IsSuccess, Is.True);
        Assert.That(coordinator.Select(started.Value, new(2, 0)).IsFailure, Is.True);
        await coordinator.DisposeAsync();
        Assert.That(runner.Pin.ToArray(), Is.All.EqualTo((byte)0));
        Assert.That(coordinator.Read(started.Value).Value.Choices, Is.Empty);
    }

    [Test]
    public async Task CancellationAndConcurrentDisposalWaitForActualWorkerCompletion()
    {
        var bundle = await Bundle();
        var runner = new GuidedVerificationTestRunner { Block = true };
        var coordinator = new GuidedVerificationCoordinator(runner, TimeProvider.System);
        var started = coordinator.Start(Request(), bundle, new byte[] { 1, 2, 3, 4 }, []);
        Assert.That(
            started.IsSuccess,
            Is.True,
            started.IsFailure ? started.Error.Code.ToString() : string.Empty
        );
        await runner.Ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(coordinator.Cancel(started.Value), Is.True);
        Assert.That(coordinator.Start(Request(), bundle, [], []).IsFailure, Is.True);
        var first = coordinator.DisposeAsync().AsTask();
        var second = coordinator.DisposeAsync().AsTask();
        Assert.That(first.IsCompleted, Is.False);
        Assert.That(second.IsCompleted, Is.False);
        runner.Release.TrySetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(runner.Pin.ToArray(), Is.All.EqualTo((byte)0));
        Assert.That(
            coordinator.Read(started.Value).Value.Error.Value,
            Is.EqualTo(VerificationErrorCodes.Cancelled)
        );
    }

    [Test]
    public async Task UnsupportedPolicyNeverStartsWorker()
    {
        var bundle = await Bundle();
        var runner = new GuidedVerificationTestRunner();
        await using var coordinator = new GuidedVerificationCoordinator(
            runner,
            TimeProvider.System
        );
        Assert.That(
            coordinator
                .Start(Request() with { ProfileId = "unapproved-profile" }, bundle, [], [])
                .IsFailure,
            Is.True
        );
        Assert.That(runner.Invocations, Is.Zero);
    }

    [Test]
    public async Task TwicRejectsPinAndContactlessWithoutPrivacyKeyBeforeReaderAccess()
    {
        var bundle = await Bundle();
        var runner = new GuidedVerificationTestRunner();
        await using var coordinator = new GuidedVerificationCoordinator(
            runner,
            TimeProvider.System
        );
        Assert.That(
            coordinator
                .Start(
                    Request() with
                    {
                        Kind = GuidedCredentialKind.Twic,
                    },
                    bundle,
                    new byte[] { 1 },
                    []
                )
                .IsFailure,
            Is.True
        );
        Assert.That(
            coordinator
                .Start(
                    Request() with
                    {
                        Kind = GuidedCredentialKind.Twic,
                        InterfaceType = PivInterfaceType.Contactless,
                    },
                    bundle,
                    [],
                    []
                )
                .IsFailure,
            Is.True
        );
        Assert.That(runner.Invocations, Is.Zero);
    }

    [Test]
    public async Task UnknownOperationCannotCancelOrSelectActiveOperation()
    {
        var bundle = await Bundle();
        var runner = new GuidedVerificationTestRunner();
        await using var coordinator = new GuidedVerificationCoordinator(
            runner,
            TimeProvider.System
        );
        var started = coordinator.Start(Request(), bundle, [], []);
        Assert.That(
            started.IsSuccess,
            Is.True,
            started.IsFailure ? started.Error.Code.ToString() : string.Empty
        );
        await runner.Ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(coordinator.Read(Guid.NewGuid()).HasNoValue, Is.True);
        Assert.That(coordinator.Cancel(Guid.NewGuid()), Is.False);
        Assert.That(coordinator.Select(Guid.NewGuid(), new(2, 0)).IsFailure, Is.True);
        Assert.That(
            coordinator.Read(started.Value).Value.Phase,
            Is.EqualTo(GuidedVerificationPhase.AwaitingFinger)
        );
    }

    private static GuidedVerificationRequest Request() =>
        new(
            Guid.NewGuid(),
            PivInterfaceType.Contact,
            "fixture-device",
            GuidedCredentialKind.Piv,
            CredentialEvidenceCodec.CardAuthenticationProfile,
            1,
            ImmutableArray<ImmutableArray<byte>>.Empty,
            ImmutableArray<ImmutableArray<byte>>.Empty
        );

    private static async Task<VerifiedBundleSnapshot> Bundle()
    {
        var fixture = new VerificationBundleFixture(
            policyOverride: new VerificationPolicySnapshot(
                "guided-fixture",
                1,
                TimeSpan.FromHours(24),
                TimeSpan.FromSeconds(60),
                true,
                true,
                true,
                ImmutableArray.Create(CredentialEvidenceCodec.CardAuthenticationProfile)
            )
        );
        var verified = await fixture.Verify();
        Assert.That(verified.IsSuccess, Is.True);
        return verified.Value;
    }
}
