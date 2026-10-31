namespace OpenPhysical.Domains.Images;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// Represents the format of an image.
/// </summary>
public enum ImageFormat
{
    /// <summary>
    /// Unknown or unsupported format.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// JPEG format.
    /// </summary>
    Jpeg = 1,

    /// <summary>
    /// JPEG 2000 format.
    /// </summary>
    Jpeg2000 = 2,

    /// <summary>
    /// PNG format.
    /// </summary>
    Png = 3,

    /// <summary>
    /// TIFF format.
    /// </summary>
    Tiff = 4,

    /// <summary>
    /// BMP format.
    /// </summary>
    Bmp = 5,
}
