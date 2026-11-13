// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>The operator-visible finger plan and acceptance actions.</summary>
public interface IFingerprintEnrollment : INotifyPropertyChanged
{
    /// <summary>Gets or sets whether two distinct fingers are required.</summary>
    bool TwoFingers { get; set; }

    /// <summary>Gets selectable finger identities.</summary>
    ImmutableArray<FingerChoice> Fingers { get; }

    /// <summary>Gets or sets the primary finger.</summary>
    FingerChoice PrimaryFinger { get; set; }

    /// <summary>Gets or sets the secondary finger.</summary>
    FingerChoice SecondaryFinger { get; set; }

    /// <summary>Gets the selected capture slot.</summary>
    FingerChoice ActiveFinger { get; }

    /// <summary>Gets whether the primary slot is selected.</summary>
    bool PrimaryActive { get; }

    /// <summary>Gets whether the secondary slot is selected.</summary>
    bool SecondaryActive { get; }

    /// <summary>Gets whether the selected capture is accepted.</summary>
    bool ActiveAccepted { get; }

    /// <summary>Gets the explicitly accepted captures in primary/secondary order.</summary>
    ImmutableArray<CapturedFingerprint> AcceptedCaptures { get; }

    /// <summary>Gets whether a slot operation is running.</summary>
    bool IsBusy { get; }

    /// <summary>Gets the primary slot acceptance state.</summary>
    string PrimaryLabel { get; }

    /// <summary>Gets the secondary slot acceptance state.</summary>
    string SecondaryLabel { get; }

    /// <summary>Gets the accepted-capture progress.</summary>
    string Progress { get; }

    /// <summary>Gets whether every finger is accepted.</summary>
    bool Complete { get; }

    /// <summary>Gets the next operator action.</summary>
    string Instructions { get; }

    /// <summary>Gets the accept-current-capture command.</summary>
    IAsyncRelayCommand AcceptCommand { get; }

    /// <summary>Gets the select-primary command.</summary>
    IAsyncRelayCommand PrimarySlotCommand { get; }

    /// <summary>Gets the select-secondary command.</summary>
    IAsyncRelayCommand SecondarySlotCommand { get; }

    /// <summary>Gets the retry-current-slot command.</summary>
    IAsyncRelayCommand ResetCaptureCommand { get; }

    /// <summary>Gets whether the current selected pending capture can be adjusted before acceptance.</summary>
    bool CanAdjustCapture { get; }

    /// <summary>Gets the edit-pending-original command, which never accepts a finger.</summary>
    IAsyncRelayCommand AdjustCaptureCommand { get; }

    /// <summary>Gets the selected pending capture's local Apply/Cancel editor.</summary>
    CaptureAdjustmentEditorViewModel Adjustment { get; }
}
