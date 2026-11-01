// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Imaging;

/// <summary>Stores an sRGB pixel with straight, unassociated alpha.</summary>
public struct RgbaPixel
{
    /// <summary>Gets or sets the red sample.</summary>
    public byte R;

    /// <summary>Gets or sets the green sample.</summary>
    public byte G;

    /// <summary>Gets or sets the blue sample.</summary>
    public byte B;

    /// <summary>Gets or sets alpha, from zero for transparent to 255 for opaque.</summary>
    public byte A;

    /// <summary>Creates a pixel from explicit colour samples.</summary>
    /// <param name="red">Red sample.</param>
    /// <param name="green">Green sample.</param>
    /// <param name="blue">Blue sample.</param>
    /// <param name="alpha">Straight alpha sample.</param>
    public RgbaPixel(byte red, byte green, byte blue, byte alpha = 255)
    {
        R = red;
        G = green;
        B = blue;
        A = alpha;
    }

    /// <summary>Creates an opaque grayscale pixel.</summary>
    /// <param name="gray">Sample used for all three colour channels.</param>
    public RgbaPixel(byte gray)
        : this(gray, gray, gray) { }

    /// <summary>Converts a standard colour value to straight-alpha pixel samples.</summary>
    /// <param name="color">Colour to convert.</param>
    /// <returns>The equivalent sRGB pixel.</returns>
    public static implicit operator RgbaPixel(System.Drawing.Color color) =>
        new(color.R, color.G, color.B, color.A);
}
