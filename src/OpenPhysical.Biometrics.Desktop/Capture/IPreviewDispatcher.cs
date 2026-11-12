// SPDX-License-Identifier: GPL-2.0-or-later

using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Marshals bounded preview presentations and capture status to the UI.</summary>
public interface IPreviewDispatcher
{
    /// <summary>Completes after the queued UI callback renders or discards the update.</summary>
    /// <param name="update">Generation-guarded preview presentation.</param>
    /// <returns>Presentation success or a typed UI failure.</returns>
    Task<UnitResult<DesktopError>> DisplayAsync(PreviewUpdate update);

    /// <summary>Presents a scanner connection or acquisition status.</summary>
    /// <param name="error">Typed status without captured pixels.</param>
    /// <returns>Status presentation success or a typed UI failure.</returns>
    Task<UnitResult<DesktopError>> ReportAsync(DesktopError error);
}
