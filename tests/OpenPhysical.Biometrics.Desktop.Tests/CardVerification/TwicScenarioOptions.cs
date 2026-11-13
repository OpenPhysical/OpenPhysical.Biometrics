// SPDX-License-Identifier: GPL-2.0-or-later
using OpenPhysical.Middleware.Domain.Twic;

namespace OpenPhysical.Middleware.Tests;

internal sealed record TwicScenarioOptions(
    TwicCardModel Generation,
    bool Forged,
    bool Cancelled,
    bool Attended,
    string Path
);
