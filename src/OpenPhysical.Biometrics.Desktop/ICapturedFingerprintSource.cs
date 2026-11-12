// SPDX-License-Identifier: GPL-2.0-or-later
using System.ComponentModel;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Exposes the current frozen capture to dependent workflows.</summary>
public interface ICapturedFingerprintSource : INotifyPropertyChanged
{
    /// <summary>Gets the exact processed capture, or no value while acquisition is live.</summary>
    Maybe<CapturedFingerprint> Captured { get; }

    /// <summary>Gets the exact presence-gated live frame, independently of its UI bitmap.</summary>
    Maybe<DisplayedFingerprintFrame> LiveFrame { get; }
}
