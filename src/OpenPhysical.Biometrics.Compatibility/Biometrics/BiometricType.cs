namespace OpenPhysical.Domains.Biometrics;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// Type of biometric data as defined in ISO/IEC 19785-1:2015 Table 10.
/// </summary>
public enum BiometricType : byte
{
    /// <summary>
    /// No information given (0x00).
    /// </summary>
    NoInformationGiven = 0x00,

    /// <summary>
    /// Scanned image (0x01).
    /// </summary>
    ScannedImage = 0x01,

    /// <summary>
    /// Facial features (0x02) - Used for facial images.
    /// </summary>
    FacialFeatures = 0x02,

    /// <summary>
    /// Voice (0x04).
    /// </summary>
    Voice = 0x04,

    /// <summary>
    /// Fingerprint (0x08) - Used for fingerprint minutiae and images.
    /// </summary>
    Fingerprint = 0x08,

    /// <summary>
    /// Iris (0x10).
    /// </summary>
    Iris = 0x10,

    /// <summary>
    /// Retina (0x20).
    /// </summary>
    Retina = 0x20,

    /// <summary>
    /// Hand geometry (0x40).
    /// </summary>
    HandGeometry = 0x40,

    /// <summary>
    /// Signature dynamics (0x80).
    /// </summary>
    SignatureDynamics = 0x80,
}
