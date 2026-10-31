namespace OpenPhysical.Domains.Cbeff.Formats.Incits378;

/// <summary>Identifies the two-bit minutia type encoded by INCITS 378-2004.</summary>
public enum MinutiaType : byte
{
    /// <summary>A minutia whose type is unspecified.</summary>
    Other = 0,

    /// <summary>The point at which a ridge ends.</summary>
    RidgeEnding = 1,

    /// <summary>The point at which a ridge divides.</summary>
    Bifurcation = 2,
}
