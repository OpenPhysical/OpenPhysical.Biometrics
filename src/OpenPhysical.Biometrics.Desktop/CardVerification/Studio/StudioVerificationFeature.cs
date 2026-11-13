// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Controls;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

internal sealed class StudioVerificationFeature(StudioCardVerificationViewModel model)
    : IStudioWorkspaceFeature
{
    public string Title => "Check card";
    public Control Content { get; } = new StudioCardVerificationView(model);

    public Task<UnitResult<DesktopError>> ActivateAsync() => model.ActivateAsync();

    public Task<UnitResult<DesktopError>> PauseAsync() => model.PauseAsync();

    public Task<UnitResult<DesktopError>> StopAsync() => model.StopAsync();
}
