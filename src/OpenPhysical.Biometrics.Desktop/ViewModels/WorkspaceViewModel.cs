// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

/// <summary>The operator workspace sharing capture, comparison, export and settings state.</summary>
/// <param name="capture">Fingerprint capture workflow.</param>
/// <param name="export">Current person and accepted artifacts.</param>
/// <param name="inspection">Reference inspection and comparison workflow.</param>
/// <param name="settings">Persistent operator preferences.</param>
public sealed class WorkspaceViewModel(
    CaptureViewModel capture,
    ExportViewModel export,
    InspectionViewModel inspection,
    SettingsViewModel settings
)
{
    /// <summary>Gets fingerprint capture controls and review state.</summary>
    public CaptureViewModel Capture { get; } = capture;

    /// <summary>Gets the current person and save workflow.</summary>
    public ExportViewModel Export { get; } = export;

    /// <summary>Gets reference inspection and comparison controls.</summary>
    public InspectionViewModel Inspection { get; } = inspection;

    /// <summary>Gets persistent operator preferences.</summary>
    public SettingsViewModel Settings { get; } = settings;
}
