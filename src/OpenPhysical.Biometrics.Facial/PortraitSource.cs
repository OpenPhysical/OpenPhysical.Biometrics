// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

// The decoded capture and the hash of the encoded bytes it came from.
internal sealed record PortraitSource(string Hash, PortraitPixels Image);
