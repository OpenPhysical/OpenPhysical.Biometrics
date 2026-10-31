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
/// Represents common EXIF tags.
/// </summary>
public enum ExifTag
{
    // Image structure
    /// <summary>Image width in pixels.</summary>
    ImageWidth = 0x0100,

    /// <summary>Image height in pixels.</summary>
    ImageHeight = 0x0101,

    /// <summary>Number of bits allocated to each colour sample.</summary>
    BitsPerSample = 0x0102,

    /// <summary>Compression scheme used for the image data.</summary>
    Compression = 0x0103,

    /// <summary>Colour model used to interpret image samples.</summary>
    PhotometricInterpretation = 0x0106,

    /// <summary>Orientation of the image relative to its stored pixel rows.</summary>
    Orientation = 0x0112,

    /// <summary>Number of colour samples in each pixel.</summary>
    SamplesPerPixel = 0x0115,

    /// <summary>Horizontal sampling density.</summary>
    XResolution = 0x011A,

    /// <summary>Vertical sampling density.</summary>
    YResolution = 0x011B,

    /// <summary>Unit used by the horizontal and vertical resolution fields.</summary>
    ResolutionUnit = 0x0128,

    // Recording offset
    /// <summary>File offsets of the encoded image strips.</summary>
    StripOffsets = 0x0111,

    /// <summary>Number of image rows in each strip.</summary>
    RowsPerStrip = 0x0116,

    /// <summary>Encoded byte length of each image strip.</summary>
    StripByteCounts = 0x0117,

    /// <summary>File offset of the JPEG thumbnail.</summary>
    JPEGInterchangeFormat = 0x0201,

    /// <summary>Length of the JPEG thumbnail in bytes.</summary>
    JPEGInterchangeFormatLength = 0x0202,

    // Image data characteristics
    /// <summary>Transfer function used to map stored samples to intensity.</summary>
    TransferFunction = 0x012D,

    /// <summary>Chromaticity of the reference white point.</summary>
    WhitePoint = 0x013E,

    /// <summary>Chromaticities of the primary colours.</summary>
    PrimaryChromaticities = 0x013F,

    /// <summary>Coefficients used to convert RGB values to YCbCr.</summary>
    YCbCrCoefficients = 0x0211,

    /// <summary>Reference black and white sample values.</summary>
    ReferenceBlackWhite = 0x0214,

    // Other tags
    /// <summary>Date and time when the image file was modified.</summary>
    DateTime = 0x0132,

    /// <summary>Text description supplied for the image.</summary>
    ImageDescription = 0x010E,

    /// <summary>Manufacturer of the capture equipment.</summary>
    Make = 0x010F,

    /// <summary>Model of the capture equipment.</summary>
    Model = 0x0110,

    /// <summary>Software used to create or modify the image.</summary>
    Software = 0x0131,

    /// <summary>Person credited with creating the image.</summary>
    Artist = 0x013B,

    /// <summary>Copyright information supplied with the image.</summary>
    Copyright = 0x8298,

    // EXIF IFD tags
    /// <summary>Offset of the EXIF metadata directory.</summary>
    ExifIFDPointer = 0x8769,

    /// <summary>Camera exposure time in seconds.</summary>
    ExposureTime = 0x829A,

    /// <summary>Lens aperture expressed as an f-number.</summary>
    FNumber = 0x829D,

    /// <summary>Camera exposure program used during capture.</summary>
    ExposureProgram = 0x8822,

    /// <summary>Spectral sensitivity of the capture equipment.</summary>
    SpectralSensitivity = 0x8824,

    /// <summary>ISO speed rating used during capture.</summary>
    ISOSpeedRatings = 0x8827,

    /// <summary>Date and time of the original capture.</summary>
    DateTimeOriginal = 0x9003,

    /// <summary>Date and time when the image was digitized.</summary>
    DateTimeDigitized = 0x9004,

    /// <summary>Shutter speed represented in APEX units.</summary>
    ShutterSpeedValue = 0x9201,

    /// <summary>Lens aperture represented in APEX units.</summary>
    ApertureValue = 0x9202,

    /// <summary>Scene brightness represented in APEX units.</summary>
    BrightnessValue = 0x9203,

    /// <summary>Exposure compensation represented in APEX units.</summary>
    ExposureBiasValue = 0x9204,

    /// <summary>Maximum lens aperture represented in APEX units.</summary>
    MaxApertureValue = 0x9205,

    /// <summary>Distance from the camera to the subject in metres.</summary>
    SubjectDistance = 0x9206,

    /// <summary>Metering mode used to determine exposure.</summary>
    MeteringMode = 0x9207,

    /// <summary>Type of illumination used during capture.</summary>
    LightSource = 0x9208,

    /// <summary>Flash status and mode used during capture.</summary>
    Flash = 0x9209,

    /// <summary>Lens focal length in millimetres.</summary>
    FocalLength = 0x920A,

    /// <summary>Comment supplied with the image.</summary>
    UserComment = 0x9286,

    // GPS IFD tags
    /// <summary>Offset of the GPS metadata directory.</summary>
    GPSIFDPointer = 0x8825,

    /// <summary>Reference hemisphere for the recorded latitude.</summary>
    GPSLatitudeRef = 0x0001,

    /// <summary>Latitude of the capture location.</summary>
    GPSLatitude = 0x0002,

    /// <summary>Reference hemisphere for the recorded longitude.</summary>
    GPSLongitudeRef = 0x0003,

    /// <summary>Longitude of the capture location.</summary>
    GPSLongitude = 0x0004,

    /// <summary>Reference indicating altitude above or below sea level.</summary>
    GPSAltitudeRef = 0x0005,

    /// <summary>Altitude of the capture location.</summary>
    GPSAltitude = 0x0006,

    /// <summary>UTC time reported by the GPS receiver.</summary>
    GPSTimeStamp = 0x0007,

    /// <summary>Satellites used by the GPS receiver.</summary>
    GPSSatellites = 0x0008,

    /// <summary>Status of the GPS measurement.</summary>
    GPSStatus = 0x0009,

    /// <summary>Two-dimensional or three-dimensional GPS measurement mode.</summary>
    GPSMeasureMode = 0x000A,

    /// <summary>Dilution of precision reported by the GPS receiver.</summary>
    GPSDOP = 0x000B,

    /// <summary>Unit used for the recorded GPS speed.</summary>
    GPSSpeedRef = 0x000C,

    /// <summary>Speed reported by the GPS receiver.</summary>
    GPSSpeed = 0x000D,

    /// <summary>True-north or magnetic-north reference for the recorded track.</summary>
    GPSTrackRef = 0x000E,

    /// <summary>Direction of movement reported by the GPS receiver.</summary>
    GPSTrack = 0x000F,
}
