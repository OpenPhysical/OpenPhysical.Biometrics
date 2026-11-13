// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Packages;

/// <summary>Owns cancellable package inspection windows and releases them with the person session.</summary>
public sealed class DesktopPackageReviewPresenter : IDisposable
{
    private readonly Window owner;
    private readonly ExportViewModel export;
    private Maybe<Window> window;
    private Maybe<PackageReviewViewModel> model;
    private Maybe<CancellationTokenSource> pending;
    private long revision;
    private bool disposed;

    private DesktopPackageReviewPresenter(Window owner, ExportViewModel export)
    {
        this.owner = owner;
        this.export = export;
        export.PropertyChanged += Changed;
        owner.Closed += OwnerClosed;
        Refresh();
    }

    /// <summary>Attaches one read-only viewer to the export model's opened package.</summary>
    /// <param name="owner">The owning Studio window.</param>
    /// <param name="export">The package-opening session.</param>
    /// <returns>An owned subscription that must be disposed with the window.</returns>
    public static DesktopPackageReviewPresenter Attach(Window owner, ExportViewModel export) =>
        new(owner, export);

    private void Changed([AllowNull] object sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(ExportViewModel.OpenedPackage))
            return;
        if (Dispatcher.UIThread.CheckAccess())
            Refresh();
        else
            Dispatcher.UIThread.Post(() => Refresh());
    }

    private UnitResult<DesktopError> Refresh()
    {
        if (disposed)
            return UnitResult.Success<DesktopError>();
        revision++;
        CancelPending();
        CloseWindow();
        var package = export.OpenedPackage;
        if (package.HasValue)
        {
            var cancellation = new CancellationTokenSource();
            pending = cancellation;
            _ = LoadAsync(package.Value, revision, cancellation);
        }
        return UnitResult.Success<DesktopError>();
    }

    private async Task LoadAsync(
        BiometricPackage package,
        long expected,
        CancellationTokenSource cancellation
    )
    {
        var token = cancellation.Token;
        var loaded = await DesktopEdge.TryAsync(
            () =>
                Task.Run(() =>
                    PackageReviewDocument
                        .FromPackage(package, token)
                        .Bind(PackageReviewViewModel.Create)
                ),
            "Package.Inspection",
            "The saved person could not be inspected."
        );
        var result = loaded.Bind(value => value);
        if (disposed || expected != revision || token.IsCancellationRequested)
        {
            if (result.IsSuccess)
                result.Value.Dispose();
            cancellation.Dispose();
            return;
        }
        pending = Maybe<CancellationTokenSource>.None;
        cancellation.Dispose();
        if (result.IsFailure)
            PresentError(result.Error);
        else
            Present(result.Value);
    }

    private UnitResult<DesktopError> Present(PackageReviewViewModel value)
    {
        var opened = DesktopEdge.Try(
            () =>
            {
                var view = new PackageReviewWindow { DataContext = value };
                view.Closed += ReviewClosed;
                model = value;
                window = view;
                view.Show(owner);
                return true;
            },
            "Package.Window",
            "The saved person window could not be opened."
        );
        if (opened.IsFailure)
        {
            CloseWindow();
            value.Dispose();
            return opened.Error;
        }
        return UnitResult.Success<DesktopError>();
    }

    private UnitResult<DesktopError> PresentError(DesktopError error)
    {
        var opened = DesktopEdge.Try(
            () =>
            {
                var view = new Window
                {
                    Title = "Package inspection · Biometric Studio",
                    Width = 520,
                    Height = 240,
                    Content = new TextBlock
                    {
                        Text = error.Message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Margin = new Thickness(24),
                    },
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                };
                view.Closed += ReviewClosed;
                window = view;
                view.Show(owner);
                return true;
            },
            "Package.Window",
            "The package inspection result could not be shown."
        );
        return opened.IsSuccess ? UnitResult.Success<DesktopError>() : opened.Error;
    }

    private UnitResult<DesktopError> CancelPending()
    {
        if (pending.HasValue)
            pending.Value.Cancel();
        pending = Maybe<CancellationTokenSource>.None;
        return UnitResult.Success<DesktopError>();
    }

    private UnitResult<DesktopError> CloseWindow(bool close = true)
    {
        if (window.HasValue)
        {
            var closing = window.Value;
            window = Maybe<Window>.None;
            closing.Closed -= ReviewClosed;
            closing.ClearValue(StyledElement.DataContextProperty);
            if (close)
                DesktopEdge.Try(
                    () =>
                    {
                        closing.Close();
                        return true;
                    },
                    "Package.Window",
                    "The inspection window could not close."
                );
        }
        if (model.HasValue)
            model.Value.Dispose();
        model = Maybe<PackageReviewViewModel>.None;
        return UnitResult.Success<DesktopError>();
    }

    private void ReviewClosed([AllowNull] object sender, EventArgs args) => CloseWindow(false);

    private void OwnerClosed([AllowNull] object sender, EventArgs args) => Dispose();

    /// <summary>Cancels pending decoding, closes the viewer, and releases display pixels.</summary>
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        revision++;
        export.PropertyChanged -= Changed;
        owner.Closed -= OwnerClosed;
        CancelPending();
        CloseWindow();
    }
}
