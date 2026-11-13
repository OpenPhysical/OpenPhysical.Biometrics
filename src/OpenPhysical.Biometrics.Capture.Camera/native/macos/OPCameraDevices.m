// SPDX-License-Identifier: GPL-2.0-or-later
#import "OPCameraDevices.h"
#import <CoreMedia/CoreMedia.h>

NSArray<AVCaptureDevice *> *OPBCDevices(void) {
    NSMutableArray<AVCaptureDeviceType> *types = [NSMutableArray arrayWithObject:AVCaptureDeviceTypeBuiltInWideAngleCamera];
    if (@available(macOS 14.0, *)) {
        [types addObject:AVCaptureDeviceTypeExternal];
    } else {
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
        [types addObject:AVCaptureDeviceTypeExternalUnknown];
#pragma clang diagnostic pop
    }
    return [AVCaptureDeviceDiscoverySession discoverySessionWithDeviceTypes:types
        mediaType:AVMediaTypeVideo position:AVCaptureDevicePositionUnspecified].devices;
}

static BOOL OPBCSize(CMVideoDimensions dimensions) {
    return dimensions.width > 0 && dimensions.height > 0 && dimensions.width <= 16384
        && dimensions.height <= 16384 && (int64_t)dimensions.width * dimensions.height <= 32 * 1024 * 1024;
}

static BOOL OPBCSupportsRate(AVCaptureDeviceFormat *format, CMTime duration) {
    if (!CMTIME_IS_NUMERIC(duration) || duration.value <= 0 || duration.timescale <= 0) return NO;
    for (AVFrameRateRange *range in format.videoSupportedFrameRateRanges) {
        if (CMTimeCompare(duration, range.minFrameDuration) >= 0
            && CMTimeCompare(duration, range.maxFrameDuration) <= 0) return YES;
    }
    return NO;
}

static BOOL OPBCAddMode(NSMutableArray<NSDictionary *> *modes, NSMutableSet<NSString *> *seen,
    CMVideoDimensions size, CMTime duration) {
    if (!CMTIME_IS_NUMERIC(duration) || duration.value <= 0 || duration.timescale <= 0) return YES;
    if ((double)duration.timescale / duration.value > 240) return YES;
    int64_t divisor = duration.timescale;
    int64_t remainder = duration.value;
    while (remainder != 0) {
        int64_t next = divisor % remainder;
        divisor = remainder;
        remainder = next;
    }
    duration = CMTimeMake(duration.value / divisor, (int32_t)(duration.timescale / divisor));
    if (duration.value > INT32_MAX) return YES;
    NSString *key = [NSString stringWithFormat:@"%d:%d:%d:%lld", size.width, size.height,
        duration.timescale, duration.value];
    if ([seen containsObject:key]) return YES;
    if (modes.count >= 512) return NO;
    [seen addObject:key];
    [modes addObject:@{@"Width":@(size.width), @"Height":@(size.height),
        @"FrameRateNumerator":@(duration.timescale), @"FrameRateDenominator":@(duration.value),
        @"PixelFormat":@"BGRA32"}];
    return YES;
}

NSArray<NSDictionary *> *OPBCModes(AVCaptureDevice *device) {
    NSMutableArray<NSDictionary *> *modes = [NSMutableArray array];
    NSMutableSet<NSString *> *seen = [NSMutableSet set];
    for (AVCaptureDeviceFormat *format in device.formats) {
        CMVideoDimensions size = CMVideoFormatDescriptionGetDimensions(format.formatDescription);
        if (!OPBCSize(size) || ![format.supportedColorSpaces containsObject:@(AVCaptureColorSpace_sRGB)]) continue;
        for (AVFrameRateRange *range in format.videoSupportedFrameRateRanges) {
            if (!OPBCAddMode(modes, seen, size, range.minFrameDuration)
                || !OPBCAddMode(modes, seen, size, range.maxFrameDuration)) return nil;
        }
        CMTime thirty = CMTimeMake(1, 30);
        if (OPBCSupportsRate(format, thirty) && !OPBCAddMode(modes, seen, size, thirty)) return nil;
    }
    return modes;
}

AVCaptureDeviceFormat *OPBCFormat(AVCaptureDevice *device, int width, int height, CMTime duration) {
    for (AVCaptureDeviceFormat *format in device.formats) {
        CMVideoDimensions size = CMVideoFormatDescriptionGetDimensions(format.formatDescription);
        if (OPBCSize(size) && size.width == width && size.height == height
            && OPBCSupportsRate(format, duration)
            && [format.supportedColorSpaces containsObject:@(AVCaptureColorSpace_sRGB)]) return format;
    }
    return nil;
}

BOOL OPBCFormatMatches(AVCaptureDevice *device, int width, int height, CMTime duration) {
    CMVideoDimensions size = CMVideoFormatDescriptionGetDimensions(device.activeFormat.formatDescription);
    return size.width == width && size.height == height
        && device.activeColorSpace == AVCaptureColorSpace_sRGB
        && CMTimeCompare(device.activeVideoMinFrameDuration, duration) == 0
        && CMTimeCompare(device.activeVideoMaxFrameDuration, duration) == 0;
}

int OPBCPermission(void) {
    AVAuthorizationStatus status = [AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeVideo];
    if (status == AVAuthorizationStatusAuthorized) return OPBC_OK;
    if (status != AVAuthorizationStatusNotDetermined) return OPBC_PERMISSION;
    dispatch_semaphore_t completed = dispatch_semaphore_create(0);
    __block BOOL granted = NO;
    [AVCaptureDevice requestAccessForMediaType:AVMediaTypeVideo completionHandler:^(BOOL result) {
        granted = result;
        dispatch_semaphore_signal(completed);
    }];
    if (dispatch_semaphore_wait(completed, dispatch_time(DISPATCH_TIME_NOW, 30 * NSEC_PER_SEC))) return OPBC_PERMISSION;
    return granted ? OPBC_OK : OPBC_PERMISSION;
}
