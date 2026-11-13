# Private biometric staging packages

`OpenPhysical.Biometrics.Packages` is a portable library shared by Studio and
`biotool`. It stores a person's unsigned codec outputs and expressly selected
companions in one protected `.opbio` file. These are inputs for a separate issuer
and signing tool, not signed PIV records or an identity attestation.

`BiometricPackage.Create` associates a stable person-session identifier, label,
optional external identifier, JSON acquisition evidence and ordered artifacts.
Each `PackageArtifact` has a portable relative name, immutable exact bytes, role,
format identifier and SHA-256. The owning biometric codec remains responsible for
record syntax and PIV profile validation; the package layer never invents another
INCITS or CBEFF encoder.

## Protection and secret ownership

Version 1 derives a 256-bit key with Argon2id version 1.3, 64 MiB memory, three
passes and four lanes. Each save generates an independent 16-byte salt and
12-byte AES-256-GCM nonce. The 16-byte GCM tag authenticates the entire public
header and ciphertext. The public envelope contains no person or artifact names.

`PackageSecret.Create` copies exact UTF-8 bytes without trimming or Unicode
normalization. Passphrases require at least fifteen Unicode scalar values and
are bounded to 4096 UTF-8 bytes. The caller owns its original input. Dispose the
secret when its owning application session locks or ends. Codec operations copy
the owned secret briefly and clear their passphrase copies, derived key and
temporary plaintext buffers in cleanup paths. This is best-effort managed-buffer
cleanup, not a guarantee that a compromised process cannot inspect memory.

No partial decrypted package is returned before GCM authentication succeeds.
Wrong passphrases and authentication failures share one error code. Even an
authenticated archive must pass member, resource, manifest, length and hash checks
before the caller receives an artifact collection. SHA-256 associations detect
corruption; the AES-GCM tag provides package authenticity under the passphrase.
Possession of that passphrase does not establish an issuer identity.

Studio may save an accepted unsigned portrait without an optical camera
measurement. Its `face.json` retains `opticalResolutionConfirmed: false` and an
`OpticalResolutionUnknown` acquisition finding; person evidence retains an empty
camera-qualification list. Encoded-record PIV metadata findings remain separate
from that unconfirmed acquisition evidence. The CLI's default record-preparation
policy continues to require optical confirmation.
An accepted face retains the exact optional optical-measurement receipt for its
camera and native mode. Saving uses that immutable receipt rather than the current
setup settings, so later revocation does not erase the accepted capture's evidence.
A Boolean confirmation without a measured receipt remains unknown in Studio.

## Version 1 envelope

All numeric values are unsigned big endian. The header is exactly 64 bytes:

| Offset | Bytes | Meaning |
| --- | --- | --- |
| 0 | 8 | ASCII `OPBIOPKG` |
| 8 | 4 | Version `1` and header length `64`, each two bytes |
| 12 | 4 | KDF `1` (Argon2id 1.3), cipher `1` (AES-256-GCM), two zero reserved bytes |
| 16 | 12 | Argon2 memory in KiB, passes and lanes, each four bytes |
| 28 | 8 | Plaintext ZIP length |
| 36 | 16 | Salt |
| 52 | 12 | GCM nonce |

The header is followed by ciphertext of the advertised ZIP length and the GCM
tag. Readers reject unknown versions/algorithms, reserved fields, inconsistent
lengths, and KDF values outside 64-256 MiB, 3-10 passes and 1-8 lanes before key
derivation. Writers use the fixed version 1 defaults above.

Inside encryption is an ordinary ZIP archive containing `manifest.json` and the
declared artifacts. Writers store members without compression. The manifest JSON
contains `version`, `personId`, `label`, `externalId` (empty when absent), `evidence`
and ordered `artifacts`. Each artifact entry contains `name`, `role`, `format`,
`length` and lowercase `sha256`. Evidence is an application-owned JSON object;
readers preserve its JSON meaning, not its whitespace layout.

Readers reject duplicate JSON property names, duplicate or case-colliding ZIP
names, traversal, absolute paths, links expressed as unsafe member names, directory
members, extra/missing members and hash/length disagreements. Nothing is extracted
to disk. Names use portable ASCII path segments, without reserved Windows device
names. JSON is bounded to 1 MiB and depth 32.

Default limits are 128 MiB authenticated plaintext/archive, 64 MiB per expanded
member and 64 ZIP members including the manifest. `PackageProtectionPolicy.Create`
allows validated limits up to 512 MiB, 256 MiB and 256 members. Expanded member
lengths are checked before allocation and are rechecked while reading.
The ZIP central directory is structurally bounded before .NET materializes its
entry objects, including a scan that counts actual headers rather than trusting
the advertised count. This version uses single-disk ZIP without ZIP64 or archive
comments; its configured sizes and member counts do not require those features.

## Private file installation

`PackageFileStore.SaveAsync(folder, package, secret, policy, token)` encrypts in
memory and generates an opaque filename. Only ciphertext enters the private
staging transaction. Failed saves leave no plaintext package or partially installed
package. `OpenAsync` bounds file input before authentication.

`PrivateFileTransaction.WriteBatchAsync` also serves existing explicit CLI output
requests, which may deliberately write an unsigned record or image. This generic
facility is separate from the encrypted package writer. It preflights the whole
batch, rejects repositories and linked destinations/ancestors, stages privately
on each destination's filesystem, flushes file contents, and installs by rename.
Failure triggers every rollback and cleanup attempt; an unrecoverable restoration
failure retains private backups and returns a failure. It provides one-file atomic
package installation and batch rollback, not a simultaneous multi-file filesystem
transaction.

New Unix directories use mode 0700 and files use 0600. Windows files and newly
created destination directories receive a protected owner-only DACL. Existing
shared ancestor permissions are not broadened or silently rewritten. Unix parent
directories are flushed after installation. Callers choose the persistent private
root; destinations inside Git repositories and symbolic-link paths are rejected.

Public regression vectors are reproduced with reference `libargon2`, Python ZIP
and `cryptography` AESGCM, independently of the C# writer. They contain only public
non-biometric test strings, including authenticated malformed archives.

## Desktop lock boundary

`BiometricPrivacySession` owns the macOS notification subscription. It forgets the
owned package passphrase synchronously before dispatching acquisition pause to the
UI thread. Concurrent inactive-session events share an in-flight pause. Closing
the session unsubscribes native observers and forgets protection; it never
automatically unlocks packages or resumes a camera.

The macOS adapter uses the public Core Foundation distributed notification API for
the platform's `com.apple.screenIsLocked` event and the actual
`NSWorkspace.notificationCenter` for the documented session-inactive event.
[Apple requires the workspace notification center](https://developer.apple.com/documentation/appkit/nsworkspace/sessiondidresignactivenotification)
for that latter subscription. The screen-lock event name is a platform convention,
not a documented Apple notification contract. Registration and process-local
workspace event delivery are tested; a real operator lock/unlock trial remains
part of platform acceptance. Unsupported operating systems return an explicit
monitoring error rather than claiming automatic lock support.

## Studio inspection

Opening a person package authenticates and decrypts it in memory. The read-only
viewer parses the retained INCITS objects and shares the existing fingerprint
companion validator with standalone template loading, including file and pixel
hashes, geometry, source identity, masks, and feature associations. It also checks
that advertised capture-plan sections have their corresponding objects.

The viewer decodes the retained portrait without face inference or re-encoding,
and uses the exact saved fingerprint points without quality assessment or
extraction. PIV metadata findings remain separate from structural file validation.
Clear person, session stop, replacing the package, and closing the viewer release
its display images. No plaintext package, image, or temporary file is written.
