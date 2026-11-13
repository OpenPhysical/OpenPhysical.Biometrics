// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

namespace OpenPhysical.Biometrics.Desktop.Tests;

[TestFixture]
internal sealed class AcceptedBiometricSnapshotTests
{
    [AvaloniaTest]
    public async Task AcceptedObjectsArePreparedInMemoryAndIdentityChangesInvalidateTheSnapshot()
    {
        var storage = new ControlledExportStorage();
        await using var context = new EnrollmentOperatorContext(
            storage,
            EnrollmentTestSuggestions.IndexPair()
        );
        context.Export.PrepareAcceptedPackage().Should().Fail();
        context.Export.Enrollment.Person.Name = "Public fixture";
        await context.AcceptAsync(context.NativeCapture(FingerPosition.RightIndex));
        await context.AcceptAsync(context.NativeCapture(FingerPosition.LeftIndex));
        var prepared = context.Export.PrepareAcceptedPackage().Should().Succeed().Which;
        prepared
            .Package.Artifacts.Should()
            .Contain(artifact => artifact.Name == "fingerprints.fmr");
        storage.Started.Task.IsCompleted.Should().BeFalse();
        context.Export.ValidateAcceptedPackage(prepared).Should().Succeed();
        context.Export.Enrollment.Person.Identifier = "changed";
        context
            .Export.ValidateAcceptedPackage(prepared)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Session.Changed");
    }

    [AvaloniaTest]
    public async Task PrivacyLockAndChangedEncodingInvalidatePreviouslyAcceptedObjects()
    {
        await using var context = new EnrollmentOperatorContext(
            new ControlledExportStorage(),
            EnrollmentTestSuggestions.IndexPair()
        );
        context.Export.Enrollment.Person.Name = "Public fixture";
        await context.AcceptAsync(context.NativeCapture(FingerPosition.RightIndex));
        await context.AcceptAsync(context.NativeCapture(FingerPosition.LeftIndex));
        var prepared = context.Export.PrepareAcceptedPackage().Value;
        context.Export.ProductType = "9";
        context
            .Export.ValidateAcceptedPackage(prepared)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Session.Changed");
        prepared = context.Export.PrepareAcceptedPackage().Value;
        context.Export.Protection.Lock();
        context
            .Export.ValidateAcceptedPackage(prepared)
            .Should()
            .Fail()
            .Which.Code.Should()
            .Be("Session.Changed");
    }
}
