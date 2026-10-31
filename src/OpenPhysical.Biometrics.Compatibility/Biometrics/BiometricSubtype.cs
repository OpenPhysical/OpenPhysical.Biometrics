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
/// Biometric subtype values as defined in ISO/IEC 19785-1:2015.
/// </summary>
/// <remarks>
/// Used to specify the location or instance of a biometric sample,
/// such as which finger or which eye.
/// </remarks>
public enum BiometricSubtype : byte
{
    /// <summary>
    /// No information available (0x00).
    /// </summary>
    NoInformation = 0x00,

    /// <summary>
    /// Right thumb (0x01).
    /// </summary>
    RightThumb = 0x01,

    /// <summary>
    /// Right index finger (0x02).
    /// </summary>
    RightIndexFinger = 0x02,

    /// <summary>
    /// Right middle finger (0x03).
    /// </summary>
    RightMiddleFinger = 0x03,

    /// <summary>
    /// Right ring finger (0x04).
    /// </summary>
    RightRingFinger = 0x04,

    /// <summary>
    /// Right little finger (0x05).
    /// </summary>
    RightLittleFinger = 0x05,

    /// <summary>
    /// Left thumb (0x06).
    /// </summary>
    LeftThumb = 0x06,

    /// <summary>
    /// Left index finger (0x07).
    /// </summary>
    LeftIndexFinger = 0x07,

    /// <summary>
    /// Left middle finger (0x08).
    /// </summary>
    LeftMiddleFinger = 0x08,

    /// <summary>
    /// Left ring finger (0x09).
    /// </summary>
    LeftRingFinger = 0x09,

    /// <summary>
    /// Left little finger (0x0A).
    /// </summary>
    LeftLittleFinger = 0x0A,

    /// <summary>
    /// Plain right thumb (0x0B).
    /// </summary>
    PlainRightThumb = 0x0B,

    /// <summary>
    /// Plain left thumb (0x0C).
    /// </summary>
    PlainLeftThumb = 0x0C,

    /// <summary>
    /// Plain right four fingers (0x0D).
    /// </summary>
    PlainRightFourFingers = 0x0D,

    /// <summary>
    /// Plain left four fingers (0x0E).
    /// </summary>
    PlainLeftFourFingers = 0x0E,

    /// <summary>
    /// Plain thumbs (both) (0x0F).
    /// </summary>
    PlainThumbs = 0x0F,

    /// <summary>
    /// Unknown finger (0x10).
    /// </summary>
    UnknownFinger = 0x10,

    /// <summary>
    /// Right iris (0x11).
    /// </summary>
    RightIris = 0x11,

    /// <summary>
    /// Left iris (0x12).
    /// </summary>
    LeftIris = 0x12,

    /// <summary>
    /// Both irises (0x13).
    /// </summary>
    BothIrises = 0x13,

    /// <summary>
    /// Right eye (0x14).
    /// </summary>
    RightEye = 0x14,

    /// <summary>
    /// Left eye (0x15).
    /// </summary>
    LeftEye = 0x15,

    /// <summary>
    /// Both eyes (0x16).
    /// </summary>
    BothEyes = 0x16,

    /// <summary>
    /// Either eye (0x17).
    /// </summary>
    EitherEye = 0x17,

    /// <summary>
    /// Unknown iris (0x20).
    /// </summary>
    UnknownIris = 0x20,

    /// <summary>
    /// Unknown face (0x30).
    /// </summary>
    UnknownFace = 0x30,

    /// <summary>
    /// Right palm (0x40).
    /// </summary>
    RightPalm = 0x40,

    /// <summary>
    /// Left palm (0x41).
    /// </summary>
    LeftPalm = 0x41,

    /// <summary>
    /// Unknown palm (0x42).
    /// </summary>
    UnknownPalm = 0x42,
}
