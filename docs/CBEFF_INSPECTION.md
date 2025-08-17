# CBEFF Inspection Tool

The OpenPhysical.Biometrics tool includes a comprehensive CBEFF (Common Biometric Exchange Formats Framework) inspection command that allows you to analyze and extract data from CBEFF-formatted biometric files.

## Features

- **Decode and Display Structure**: Shows the complete CBEFF structure including header, biometric data block, and signature
- **Format Recognition**: Automatically identifies INCITS 385 (facial image) and INCITS 378 (fingerprint) formats
- **Hex Dump**: Optional raw hex display of data sections
- **Data Extraction**: Extract the biometric data block to a separate file
- **Signature Verification**: Verify CMS signatures on signed CBEFF objects (coming soon)

## Usage

### Basic Inspection

```bash
# Inspect a CBEFF file
biometrics cbeff facial_image.bin

# Inspect with hex dump
biometrics cbeff facial_image.bin --hex
```

### Extract Biometric Data

```bash
# Extract the biometric data block (e.g., JPEG 2000 image)
biometrics cbeff facial_image.bin --extract image.jp2

# Extract fingerprint minutiae data
biometrics cbeff fingerprint.bin --extract minutiae.dat
```

### Verify Signatures

```bash
# Verify signature with a specific certificate
biometrics cbeff signed_cbeff.bin --verify --cert signer.crt
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
- Empty template detection for individuals who cannot be fingerprinted

### Signature Information
- CMS signature presence and size
- Signature type and structure

## Examples with GSA ICAM Test Data

```bash
# Inspect Golden PIV facial image
biometrics cbeff "/path/to/gsa-icam/cards/ICAM_Card_Objects/01_Golden_PIV/10 - Face Object"

# Extract the JPEG 2000 facial image
biometrics cbeff "/path/to/gsa-icam/cards/ICAM_Card_Objects/01_Golden_PIV/10 - Face Object" \
  --extract golden_piv_face.jp2
```

## Understanding the Output

### Security Options
- `0x00`: No security
- `0x08`: Integrity
- `0x0C`: Integrity and confidentiality  
- `0x0D`: Signed plaintext (most common for PIV)

### Biometric Quality Values
- `-2`: Not supported
- `-1`: Failed attempt
- `0-100`: Quality percentage
- `254`: Permanently unusable (PIV fingerprint)
- `255`: Temporarily unusable (PIV fingerprint)

### Format Types
- `0x001B/0x0501`: INCITS 385 Facial Image
- `0x001B/0x0201`: INCITS 378 Fingerprint Minutiae
- `0x0101/0x0008`: PIV Patron Format A
- `0x0101/0x0009`: PIV Patron Format B

## Integration with Other Tools

The extracted biometric data can be used with other OpenPhysical.Biometrics commands:

```bash
# Extract facial image from CBEFF
biometrics cbeff face_object.bin --extract face.jp2

# Process the extracted image for PIV compliance
biometrics process face.jp2 --output processed_face.jp2

# Analyze quality of the extracted image
biometrics quality --input face.jp2 --standard piv
```

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
- CMS signed CBEFF objects

## Error Handling

The tool provides detailed error messages for common issues:
- Invalid CBEFF header structure
- Mismatched data lengths
- Unknown biometric formats
- Corrupted signature blocks

## Future Enhancements

- Full CMS signature verification with certificate chain validation
- Support for additional biometric formats (iris, voice)
- Batch processing of multiple CBEFF files
- Export to JSON/XML for integration with other systems
- Visual representation of facial/fingerprint data
