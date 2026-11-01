// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

// An expected outcome that stops preparation and asks the caller to review the capture.
internal sealed record PortraitReview(PortraitStatus Status, PortraitFinding Finding);
