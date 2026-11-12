// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using OpenPhysical.Biometrics.Fingerprints;

[TestFixture]
internal sealed class ReferenceSelectionTests
{
    [AvaloniaTest]
    public async Task FirstAndReplacementLoadsKeepTheActualChoiceSelectedInTheComboBox()
    {
        var dependencies = new ControlledInspectionDependencies();
        var view = NewView(dependencies);
        var combo = Bind(view);
        await view.LoadCommand.ExecuteAsync(default);
        combo.SelectedItem.Should().BeSameAs(view.Reference.Views[0]);
        view.Reference.SelectedView.Finger.Should().Be(FingerPosition.RightIndex);
        dependencies.Document = ControlledInspectionDependencies.DocumentFor(
            FingerPosition.RightIndex
        );
        await view.LoadCommand.ExecuteAsync(default);
        combo.SelectedItem.Should().BeSameAs(view.Reference.Views[0]);
        dependencies.Document = ControlledInspectionDependencies.DocumentFor(
            FingerPosition.LeftIndex
        );
        await view.LoadCommand.ExecuteAsync(default);
        combo.SelectedItem.Should().BeSameAs(view.Reference.Views[0]);
        view.Reference.SelectedView.Finger.Should().Be(FingerPosition.LeftIndex);
        view.Scene.HasValue.Should().BeTrue();
        await view.StopAsync();
    }

    [AvaloniaTest]
    public async Task ReplacementKeepsTheSameFingerAndViewWhenAvailable()
    {
        var dependencies = new ControlledInspectionDependencies();
        var right = dependencies.Document.Record.Views[0];
        var left = right with { FingerPosition = (byte)FingerPosition.LeftIndex };
        dependencies.Document = dependencies.Document with
        {
            Record = dependencies.Document.Record with { Views = [right, left] },
        };
        var view = NewView(dependencies);
        var combo = Bind(view);
        await view.LoadCommand.ExecuteAsync(default);
        view.Reference.SelectedView = view.Reference.Views[1];
        dependencies.Document = dependencies.Document with
        {
            Record = dependencies.Document.Record with { Views = [left, right] },
        };
        await view.LoadCommand.ExecuteAsync(default);
        view.Reference.SelectedView.Index.Should().Be(0);
        view.Reference.SelectedView.Finger.Should().Be(FingerPosition.LeftIndex);
        combo.SelectedItem.Should().BeSameAs(view.Reference.Views[0]);
        await view.ClearPersonAsync(FingerChoice.For(FingerPosition.RightIndex));
        combo.SelectedIndex.Should().Be(-1);
        view.Scene.HasNoValue.Should().BeTrue();
        await view.StopAsync();
    }

    [AvaloniaTest]
    public async Task CredentialLoadSelectsItsActualFingerAndInvalidDocumentClearsTheSelection()
    {
        var dependencies = new ControlledInspectionDependencies();
        var credential = ControlledInspectionDependencies.DocumentFor(FingerPosition.LeftIndex);
        var view = new InspectionViewModel(
            dependencies,
            new DesktopFilePicker(),
            dependencies,
            new TemplateComparison(new FingerprintComparisonService(dependencies)),
            new CredentialReaderService(new DocumentCredentialTransport(credential))
        );
        var combo = Bind(view);
        await view.Credential.RefreshCommand.ExecuteAsync(default);
        view.Credential.Pin = "123456";
        await view.Credential.ReadCommand.ExecuteAsync(default);
        view.Reference.SelectedView.Finger.Should().Be(FingerPosition.LeftIndex);
        combo.SelectedItem.Should().BeSameAs(view.Reference.Views[0]);
        view.Scene.HasValue.Should().BeTrue();
        dependencies.Document = dependencies.Document with
        {
            Record = dependencies.Document.Record with { Views = [] },
        };
        await view.LoadCommand.ExecuteAsync(default);
        combo.SelectedIndex.Should().Be(-1);
        view.Reference.Views.Should().BeEmpty();
        view.Scene.HasNoValue.Should().BeTrue();
        view.Report.Should().Contain("ViewSelection");
        await view.StopAsync();
    }

    private static ComboBox Bind(InspectionViewModel view)
    {
        var combo = new ComboBox { DataContext = view };
        combo.Bind(ItemsControl.ItemsSourceProperty, new Binding("Reference.Views"));
        combo.Bind(
            SelectingItemsControl.SelectedItemProperty,
            new Binding("Reference.SelectedView") { Mode = BindingMode.TwoWay }
        );
        return combo;
    }

    private static InspectionViewModel NewView(ControlledInspectionDependencies dependencies) =>
        new(
            dependencies,
            new DesktopFilePicker(),
            dependencies,
            new TemplateComparison(new FingerprintComparisonService(dependencies)),
            new CredentialReaderService(
                new ScriptedCredentialTransport(new ScriptedCredentialCard())
            )
        );
}
