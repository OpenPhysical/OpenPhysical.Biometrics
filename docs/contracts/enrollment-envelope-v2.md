# Enrollment envelope v2

This contract defines the signed, recipient-encrypted `.opbio` enrollment handoff. The ASN.1 module is in [enrollment-envelope-v2.asn1](enrollment-envelope-v2.asn1). Encodings MUST be one canonical DER value with no trailing bytes.

## Header and transfer identity

The outer sequence contains a header, the ordered encrypted file sequence, and an ECDSA signature. The header is authenticated by both the HPKE associated data and the outer signature. Version is `2`; suite `1` means HPKE base mode using DHKEM(P-256, HKDF-SHA256), HKDF-SHA256, and AES-256-GCM (RFC 9180 IDs `0x0010`, `0x0001`, `0x0002`). Unknown versions or suites are rejected.

Organization, transfer, sender, recipient, and enrollment IDs are required opaque ASCII identifiers containing only letters, digits, `-`, `_`, `.`, and `:`. They are bounded to 256 characters and cannot carry names or email addresses. Sender and recipient key IDs are SHA-256 of their DER SubjectPublicKeyInfo. The IDs identify the transfer parties; key IDs identify their cryptographic keys. Revision is unsigned 32-bit. Purpose values are `1` local draft, `2` upload, `3` delivery, and `4` storage. Timestamps are whole Unix seconds; expiry follows creation by at most one year. Authorization references are supplemental opaque references, at most 64 entries of 512 characters each.

## File encryption and signatures

File index `0` is the encrypted signed origin manifest. Its ordered artifacts occupy indices `1` through `N`. The header file count includes file zero. Every file uses a fresh, single-shot RFC 9180 HPKE base context. The 65-byte encapsulated key is an uncompressed SEC1 P-256 point; the ciphertext includes the 16-byte GCM tag.

Let `H` be SHA-256 of the canonical DER header, `I` the zero-based file index, and `C` the total file count. Encode `I` and `C` as four-byte unsigned big-endian integers. HPKE `info` is the ASCII bytes `OpenPhysical.EnrollmentEnvelope.v2\0info` followed by `H || I || C`. AEAD associated data is the ASCII bytes `OpenPhysical.EnrollmentEnvelope.v2\0aad` followed by `H || I || C`. Each file has an independent encapsulation and key schedule.

The outer signature is ECDSA P-256/SHA-256. Its signer receives the digest `SHA-256(ASCII("OpenPhysical.EnrollmentEnvelope.v2\0") || DER(SEQUENCE { header, files }))`. The origin signer receives `SHA-256(ASCII("OpenPhysical.EnrollmentOriginSignature.v2\0") || DER(origin statement))`. Signatures are DER ECDSA `(r,s)` byte strings. Trusted signing keys are resolved by the corresponding SPKI fingerprint.

The immutable origin idempotency digest is `SHA-256(ASCII("OpenPhysical.EnrollmentOrigin.v2\0") || DER(origin statement))`. It excludes the origin signature and the transport envelope, so fresh transfer IDs, recipients, encapsulations, ciphertexts, and outer signatures do not change it.

## Origin statement and acceptance

The signed origin statement records organization, person, enrollment, revision, origin capture purpose, station, exact capture-policy evidence bytes, and the ordered artifact index, name, role, media type, length, and SHA-256. The same origin statement and signature are retained when an accepted enrollment is re-encrypted for another recipient.

Readers verify the outer signature before any recipient ECDH operation. They then decrypt every file, validate the origin signature and all header/origin associations, and check each artifact’s position, exact length, and SHA-256 before returning an accepted result. A failed member releases no partial artifact collection. Readers reject noncanonical or trailing DER, DER nesting deeper than 16 levels or above 2,048 elements, wrong key fingerprints, invalid or hybrid P-256 points, duplicate or reordered indices, missing files, malformed signatures, and unsupported suites.

## Resource limits

- At most 64 encrypted files including the origin manifest, so at most 63 artifacts.
- Origin manifest DER at most 1 MiB.
- Each artifact at most 64 MiB.
- Aggregate decrypted file bytes, including the origin manifest, at most 128 MiB.
- DER structure is preflighted before Bouncy Castle parsing, with a maximum depth of 16 and 2,048 elements.

Writers reject a limit violation before sealing. Readers bound the outer input and each advertised ciphertext before ECDH, then enforce the aggregate again on authenticated plaintext.
