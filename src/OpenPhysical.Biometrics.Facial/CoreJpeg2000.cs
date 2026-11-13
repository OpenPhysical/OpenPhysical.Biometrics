// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CoreJ2K.j2k.util;
using CoreJ2K.Skia;
using CoreJ2K.Util;
using CSharpFunctionalExtensions;

// CoreJ2K's factory and logger are process-wide; encoding and decoding share their ownership.
internal static class CoreJpeg2000
{
    private static readonly object Gate = new();

    internal static Result<T, FacialError> Run<T>(
        Func<Result<T, FacialError>> action,
        string code,
        string message
    ) => FacialEdge.TryBind(() => Locked(action), code, message);

    private static Result<T, FacialError> Locked<T>(Func<Result<T, FacialError>> action)
    {
        lock (Gate)
        {
            var previousLogger = FacilityManager.GetMsgLogger();
            FacilityManager.DefaultMsgLogger = new Jpeg2000MessageSink();
            try
            {
                ImageFactory.Register(new SKBitmapImageCreator());
                return action();
            }
            finally
            {
                FacilityManager.DefaultMsgLogger = previousLogger;
            }
        }
    }
}
