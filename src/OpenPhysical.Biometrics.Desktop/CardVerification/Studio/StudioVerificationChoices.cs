// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using OpenPhysical.Middleware.Domain.Trust;
using OpenPhysical.Middleware.Infrastructure.Twic;

namespace OpenPhysical.Biometrics.Desktop;

internal static class StudioVerificationChoices
{
    internal static ImmutableArray<StudioVerificationChoice> Read(VerifiedBundleSnapshot bundle)
    {
        var choices = ImmutableArray.CreateBuilder<StudioVerificationChoice>();
        foreach (var id in bundle.Policy.AllowedProfileIds)
        {
            if (id == CredentialVerificationProfiles.ManagedTestCardAuthentication)
                choices.Add(new("OpenPhysical test card", id, StudioVerificationKind.Card));
            if (id == CredentialVerificationProfiles.CardAuthentication)
            {
                choices.Add(new("PIV card", id, StudioVerificationKind.Card));
                choices.Add(
                    new("PIV card and fingerprint", id, StudioVerificationKind.PivFingerprint)
                );
            }
            if (id == CredentialVerificationProfiles.PinAuthentication)
                choices.Add(new("PIV card and PIN", id, StudioVerificationKind.Pin));
            var twic = TwicAcceptanceService.ReadProfile(bundle, id);
            if (twic.IsSuccess)
                choices.Add(
                    new(
                        $"TWIC {twic.Value.Generation}: {twic.Value.Workflow}",
                        id,
                        StudioVerificationKind.TwicFingerprint
                    )
                );
        }
        return choices.ToImmutable();
    }
}
