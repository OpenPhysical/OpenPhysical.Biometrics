// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture.Futronic;

/// <summary>Measured spatial admission counts; unobserved area is never called static.</summary>
/// <param name="ContactArea">Original selected texture area C.</param>
/// <param name="CorroboratedArea">Contact covered by compatible change or anchored geometry.</param>
/// <param name="UncorroboratedExteriorArea">Residual C components touching the exterior of filled F.</param>
/// <param name="StaticExteriorArea">Residual exterior pixels directly covered by observed-static windows.</param>
/// <param name="UnobservedExteriorArea">Residual exterior pixels without direct static or changed observation.</param>
/// <param name="LargestUncorroboratedExteriorArea">Largest connected exterior residual, without aspect constraints.</param>
public sealed record Fs50WholeContactMetrics(
    int ContactArea,
    int CorroboratedArea,
    int UncorroboratedExteriorArea,
    int StaticExteriorArea,
    int UnobservedExteriorArea,
    int LargestUncorroboratedExteriorArea
);
