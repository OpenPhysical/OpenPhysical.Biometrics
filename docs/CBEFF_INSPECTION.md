# CBEFF inspection

`biotool inspect` and the `BiometricInspector` library API decode CBEFF (Common
Biometric Exchange Formats Framework) objects, INCITS 378 fingerprint records, and
INCITS 385 facial records. Both report structure with byte offsets and PIV profile
findings.

## Features

- **Decode and display structure**: header, biometric data block, and signature block
- **Format recognition**: identifies INCITS 385 (facial image) and INCITS 378 (fingerprint) formats
- **Located diagnostics**: stable codes with byte offsets and lengths
- **JSON output**: the same report the library supplies to middleware

## Command line

```bash
# Inspect a raw record or CBEFF object
biotool inspect facial_image.bin

# Machine-readable output, with field contents only when explicitly requested
biotool inspect fingerprint.bin --json --include-sensitive

# Check validity dates against an explicit evaluation time
biotool validate signed_cbeff.bin --at 2026-10-31T12:00:00.0000000+00:00
```

## Library

Parsing returns a result rather than throwing. A success holds the immutable record
and an `InspectionReport`. A failure holds a `FormatError` whose `Code` is stable and
whose `Diagnostics` list the located problems, so a rejected record can still be
explained.

```csharp
using OpenPhysical.Biometrics.Formats.Cbeff;
using OpenPhysical.Biometrics.Piv;

var parsed = CbeffCodec.Parse(rawObject); // Result<ParsedRecord<CbeffRecord>, FormatError>
if (parsed.IsFailure)
{
    foreach (var diagnostic in parsed.Error.Diagnostics)
        Present(diagnostic.Code, diagnostic.Offset, diagnostic.Length);
    return;
}

foreach (var field in parsed.Value.Inspection.Fields)
{
    // Value is a Maybe<string>. It has a value only when
    // InspectionOptions.IncludeSensitiveValues was set.
    Present(field.Name, field.Offset, field.Length, field.Value.GetValueOrDefault("hidden"));
}

// BiometricInspector never fails; unparseable input yields a report with diagnostics.
var report = BiometricInspector.Inspect(rawObject, evaluationTime);
```

## Output Format

The tool displays a hierarchical tree structure showing:

### Header Information (88 bytes)
- Patron header version and security options
- Biometric data block (BDB) and signature block (SB) lengths
- Format owner and type (e.g., INCITS TC M1, PIV Patron Format)
- Creation and validity dates
- Biometric type, quality, and metadata
- Creator information and FASC-N

### Biometric Data Analysis
- For facial images: INCITS 385 format details, image dimensions, data type
- For fingerprints: INCITS 378 template structure, finger records
- Empty template detection for unavailable fingers

### Signature Information
- CMS signature presence and size
- Signature type and structure

## Understanding the Output

### Security Options
- `0x00`: No security
- `0x08`: Integrity
- `0x0C`: Integrity and confidentiality  
- `0x0D`: Signed plaintext (most common for PIV)

### Biometric Quality Values
- `-2`: Unsupported operation
- `-1`: Failed attempt
- `0-100`: Quality percentage
- `254`: Permanently unusable (PIV fingerprint)
- `255`: Temporarily unusable (PIV fingerprint)

### Format Types
- `0x001B/0x0501`: INCITS 385 Facial Image
- `0x001B/0x0201`: INCITS 378 Fingerprint Minutiae
- `0x0101/0x0008`: PIV Patron Format A
- `0x0101/0x0009`: PIV Patron Format B

## Technical Details

### CBEFF Structure (per SP 800-76-2)
```
+------------------------+
| CBEFF Header (88 bytes)|
+------------------------+
| Biometric Data Block   |
| (variable length)      |
+------------------------+
| Signature Block        |
| (optional, var length) |
+------------------------+
```

### Supported Formats
- PIV facial images (INCITS 385)
- PIV fingerprint templates (INCITS 378)
- Empty fingerprint templates (SP 800-76-2 Section 4.4.3)
- CMS signed CBEFF objects (structure only; cryptographic verification is external)

## Error handling

Malformed input never throws. Each failure is a `FormatError` with located diagnostics
for these common problems:

- Invalid CBEFF header structure
- Mismatched data lengths
- Unknown biometric formats
- Corrupted signature blocks

Signature verification belongs to the application's provider; see
[signature integration](SIGNATURE-INTEGRATION.md).
