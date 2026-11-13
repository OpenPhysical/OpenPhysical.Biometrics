// SPDX-License-Identifier: GPL-2.0-or-later
#ifndef OPENPHYSICAL_CAMERA_H
#define OPENPHYSICAL_CAMERA_H
#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

enum opbc_status {
    OPBC_OK = 0, OPBC_PERMISSION = 1, OPBC_UNAVAILABLE = 2,
    OPBC_UNSUPPORTED_MODE = 3, OPBC_INVALID_FRAME = 4,
    OPBC_INTERNAL = 5, OPBC_CLOSED = 6
};

// Pixels are unmirrored, top-down, opaque sRGB BGRA8, borrowed until callback returns.
typedef void (*opbc_frame_callback)(void *context, const uint8_t *pixels, size_t length,
    int width, int height, size_t stride, int64_t timestamp_ns, int status);

__attribute__((visibility("default"))) int opbc_list(char **json);
__attribute__((visibility("default"))) int opbc_open(const char *identifier, int width,
    int height, int rate_numerator, int rate_denominator, void **handle);
__attribute__((visibility("default"))) int opbc_start(void *handle,
    opbc_frame_callback callback, void *context);
__attribute__((visibility("default"))) int opbc_stop(void *handle);
__attribute__((visibility("default"))) void opbc_release(void *handle);
__attribute__((visibility("default"))) void opbc_free(void *memory);

#ifdef __cplusplus
}
#endif
#endif
