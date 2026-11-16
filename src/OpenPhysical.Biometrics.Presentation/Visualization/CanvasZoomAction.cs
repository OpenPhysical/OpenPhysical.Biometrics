// SPDX-License-Identifier: GPL-2.0-or-later
using CommunityToolkit.Mvvm.Input;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed record CanvasZoomAction(string Label, string AccessibleName, IRelayCommand Command);
