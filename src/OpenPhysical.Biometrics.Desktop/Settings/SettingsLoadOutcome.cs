// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using CSharpFunctionalExtensions;

internal sealed record SettingsLoadOutcome(
    DesktopPreferences Preferences,
    Maybe<SettingsNotice> Notice,
    Maybe<DesktopError> PersistenceFailure
);
