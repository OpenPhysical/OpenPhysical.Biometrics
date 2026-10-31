# Biometric interchange formats

`OpenPhysical.Biometrics.Formats` reads and writes the binary records used to carry PIV fingerprints and facial images. It owns the managed interchange models, while the separate fingerprint and PIV packages provide extraction, matching, signing, and policy checks.

The immutable models represent INCITS 378-2004 minutiae records, INCITS 385-2004 facial records, and the 88-byte PIV CBEFF patron header. Parsers return a `ParseResult<T>` with field locations and structural diagnostics. Writers calculate record lengths and collection counts from the supplied models.

```csharp
using OpenPhysical.Biometrics.Formats.Fingerprints;

// Start with the raw FMR bytes from the verified CBEFF biometric block.
// Parse once, then reuse this immutable record for selection and inspection.
var result = Incits378Codec.Parse(fmrBytes);
if (!result.IsSuccess)
{
    // These diagnostics describe the field and offset, without its contents.
    foreach (var error in result.Diagnostics)
        Console.WriteLine($"{error.Code} at byte {error.Offset}: {error.Message}");
    return;
}

// The finger position and view number together identify one captured impression.
// Select them explicitly rather than relying on the ordering of the card record.
var rightIndex = result.Value!.Views.Single(view =>
    view.FingerPosition == 2 && view.ViewNumber == 0);

// Unknown vendor blocks retain their exact payload bytes when the record is written.
var encoded = Incits378Codec.Write(result.Value);
```

Fingerprint coordinates use the original image's top-left origin and pixel units. Resolution fields use pixels per centimetre. INCITS 378 angles use two-degree units from zero through 179, counterclockwise from positive X. The fingerprint processing adapter performs the explicit conversion to NBIS coordinates and angles.

Inspection fields contain their names, original byte offsets, and sizes. Values are included only when the caller supplies `new InspectionOptions(IncludeSensitiveValues: true)`. Treat that report as biometric data and display it only in an explicitly requested inspection workflow.

CBEFF records preserve the original header bytes. `CbeffCodec.GetSignedData(record)` concatenates that exact header and its biometric block for detached CMS verification. Set the final signature length before signing; `WithSignatureLength` returns a new header while preserving the other fields.

The format layouts are drawn from ANSI INCITS 378-2004 sections 6.4–6.6, ANSI INCITS 385-2004 facial records, and NIST SP 800-76's PIV patron-header layout. Profile requirements and trust decisions are represented by the PIV package's validation results.
