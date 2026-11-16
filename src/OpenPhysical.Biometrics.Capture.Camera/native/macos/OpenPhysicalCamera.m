// SPDX-License-Identifier: GPL-2.0-or-later
#import "OpenPhysicalCamera.h"
#import "OPCameraDevices.h"
#import "OPCameraSession.h"
#import <Foundation/Foundation.h>
#import <stdlib.h>
#import <string.h>

static int OPBCEdge(int (^action)(void)) {
    @autoreleasepool {
        @try { return action(); }
        @catch (NSException *exception) { (void)exception; return OPBC_INTERNAL; }
    }
}

int opbc_list(char **json) {
    if (!json) return OPBC_INTERNAL;
    *json = NULL;
    return OPBCEdge(^int {
        NSMutableArray<NSDictionary *> *sources = [NSMutableArray array];
        for (AVCaptureDevice *device in OPBCDevices()) {
            NSArray<NSDictionary *> *modes = OPBCModes(device);
            if (!modes || sources.count >= 64) return OPBC_INTERNAL;
            if (modes.count > 0) [sources addObject:@{@"Id":device.uniqueID,
                @"Name":device.localizedName, @"Modes":modes}];
        }
        NSError *error = nil;
        NSData *data = [NSJSONSerialization dataWithJSONObject:sources options:0 error:&error];
        if (!data || data.length > 1024 * 1024) return OPBC_INTERNAL;
        char *bytes = malloc(data.length + 1);
        if (!bytes) return OPBC_INTERNAL;
        memcpy(bytes, data.bytes, data.length);
        bytes[data.length] = 0;
        *json = bytes;
        return OPBC_OK;
    });
}

int opbc_open(const char *identifier, int width, int height, int numerator, int denominator, void **handle) {
    if (!handle) return OPBC_INTERNAL;
    *handle = NULL;
    if (!identifier || width < 1 || height < 1 || width > 16384 || height > 16384
        || (int64_t)width * height > 32 * 1024 * 1024 || numerator < 1 || denominator < 1 || (int64_t)numerator > (int64_t)denominator * 240) return OPBC_UNSUPPORTED_MODE;
    return OPBCEdge(^int {
        int allowed = OPBCPermission();
        if (allowed != OPBC_OK) return allowed;
        NSString *selected = [NSString stringWithUTF8String:identifier];
        if (!selected) return OPBC_UNAVAILABLE;
        AVCaptureDevice *found = nil;
        for (AVCaptureDevice *device in OPBCDevices()) {
            if ([device.uniqueID isEqualToString:selected]) { found = device; break; }
        }
        if (!found) return OPBC_UNAVAILABLE;
        OPCameraSession *session = [OPCameraSession new];
        int configured = [session configure:found width:width height:height duration:CMTimeMake(denominator, numerator)];
        if (configured != OPBC_OK) { [session stop]; return configured; }
        *handle = (__bridge_retained void *)session;
        return OPBC_OK;
    });
}

int opbc_start(void *handle, opbc_frame_callback callback, void *context) {
    if (!handle || !callback) return OPBC_CLOSED;
    return OPBCEdge(^int { return [(__bridge OPCameraSession *)handle start:callback context:context]; });
}

int opbc_stop(void *handle) {
    return !handle ? OPBC_CLOSED : OPBCEdge(^int { return [(__bridge OPCameraSession *)handle stop]; });
}

void opbc_release(void *handle) {
    if (!handle) return;
    (void)OPBCEdge(^int {
        OPCameraSession *session = CFBridgingRelease(handle);
        return [session stop];
    });
}

void opbc_free(void *memory) { free(memory); }
