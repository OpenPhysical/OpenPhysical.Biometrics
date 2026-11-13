# Studio card verification

The **Check card** tab uses the existing signed configuration, protected pairing,
operator identity and card/holder verifiers. It does not issue credentials or grant
access. Signing remains in the separate Signing application.

## Administrator setup

Card checking currently uses the protected macOS identity provider. Studio opens
its private `LocalApplicationData/OpenPhysical/Verification` profile only when the
operator opens the Check card tab. Its `verification-state.db` has independent
migrations; it does not open or migrate the issuance `workstation.db` database.
One runtime owns the profile lease. Leaving the task cancels and drains card/scanner
work, clears transient PIN and TWIC privacy-key buffers, signs out and releases the
profile.

1. Provide an administrator-issued pairing invitation and the HTTPS server address.
   The invitation pins the server certificate. Studio imports the real invitation
   envelope; it never accepts an unpinned server connection.
2. Install the operator's protected identity using the existing workstation setup
   tools. Studio lists installed identities and uses them for sign-in. It has no raw
   private-key or password fallback.
3. Configure the workstation's trusted clock confidence. Set
   `OPENPHYSICAL_VERIFICATION_CLOCK_UNCERTAINTY_SECONDS` to the justified uncertainty
   bound before launching Studio. The accepted range is **0 through 300 seconds**.
   This is a declaration of an independently established bound, not a time-sync
   mechanism. Do not choose zero without evidence. An absent, invalid or out-of-range
   value leaves confidence unknown and blocks card checks. Updating signed settings
   cannot establish clock confidence. Restart Studio after changing this setting.
4. Publish the required signed ceremony profiles, trust anchors and fresh signed
   revocation/cancellation evidence. Studio only offers methods allowed by the
   authenticated configuration. Unknown profiles do not create new operations.

## Operator workflow

Connect this computer with the invitation, sign in, then load current signed
settings. Insert the card and select the available check. PIV PIN input is explicit
and sent once; a failed check clears the input and requires another deliberate
submission. Fingerprint checks authenticate the reference on the retained card
before presenting enrolled-finger choices. Select a finger, then follow the scanner
instruction. Cancel or leave the tab to drain the operation before releasing the
card and scanner.

Additional card settings accept public issuer/content-signer certificates only as
untrusted path-building candidates. They do not add trust anchors. Contactless TWIC
checks require a separately acquired privacy-key container; contact acquisition
uses the existing bounded TWIC path. The selected MARSEC level is explicit.

The result keeps card proof, holder verification, status, issuer trust and signed
policy separate. Missing evidence remains insufficient evidence. Refreshing settings
never manufactures a card or holder proof and never retries a PIN automatically.

The same capture-side services remain available through Studio's `verify-card`
command mode. The general headless CLI does not load capture engines or drivers.
