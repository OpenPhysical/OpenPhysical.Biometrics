# macOS camera bridge

`build.sh /absolute/output/libopenphysical_camera.dylib` builds the ARC bridge
with the installed macOS SDK. The default architecture is arm64; set
`OPBC_ARCHS="arm64 x86_64"` for a universal library. Deployment targets macOS 13.
Apple frameworks remain operating-system dependencies.

The exported ABI is declared in `OpenPhysicalCamera.h`. Status values are
0 success, 1 permission, 2 unavailable, 3 unsupported mode, 4 invalid frame,
5 internal failure, and 6 closed. `opbc_list` returns an owned NUL-terminated
UTF-8 JSON array, freed with `opbc_free`. Catalog reads never set an active
format. Modes use canonical rational frame rates and eight-bit `BGRA32` output.

Each successful open owns one ARC bridge retain. Serialize operations on that
handle. Stop and release from a control thread outside the frame callback.
`opbc_stop` detaches the delegate, stops capture, and drains the serial frame
queue before returning success. `opbc_release` also stops and drains before
releasing the bridge retain, including after a failed start.

Frame bytes are borrowed only until the callback returns. The managed adapter
must copy rows during the callback and retain its callback/context until stop
and release complete. Output is unmirrored sRGB BGRA8, using the actual pixel
buffer geometry and row stride. A reused Core Image context performs color
conversion without a geometric transform, and converted buffers are cleared
after callback delivery. Source buffers belong to AVFoundation.

The device's exact active format, frame durations and supported sRGB color
space are set under its configuration lock and checked after commit/start and
on every frame. No session preset is assigned: InputPriority and several
suggested automatic-format controls are unavailable on macOS. The bridge uses
only APIs verified in the installed macOS SDK headers.

Enumeration is bounded to 64 sources, 512 modes per source, 240 fps, and 1 MiB
of JSON. Capture dimensions are bounded to 32 megapixels. No images or camera
frames are written to disk by this bridge.
