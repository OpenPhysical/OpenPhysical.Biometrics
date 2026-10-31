using System.Collections.Immutable;

namespace OpenPhysical.Biometrics.Formats.Cbeff;

/// <summary>Contains an immutable PIV CBEFF header, biometric block, and detached security block.</summary>
/// <param name="Header">The exact header used for signed-content construction.</param>
/// <param name="BiometricData">Raw biometric interchange record bytes.</param>
/// <param name="Signature">Detached security-block bytes, or an initialized empty array.</param>
public sealed record CbeffRecord(
    CbeffHeader Header,
    ImmutableArray<byte> BiometricData,
    ImmutableArray<byte> Signature
);
