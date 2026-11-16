// SPDX-License-Identifier: GPL-2.0-or-later
#import "OPCameraDelegate.h"
#import "OPCameraSession.h"
#import <CoreImage/CoreImage.h>
#import <CoreVideo/CoreVideo.h>
#import <string.h>

@interface OPCameraDelegate ()
@property(nonatomic, weak) OPCameraSession *owner;
@property(nonatomic) opbc_frame_callback callback;
@property(nonatomic) void *context;
@property(nonatomic) CIContext *renderer;
@property(nonatomic) CVPixelBufferPoolRef pool;
@property(nonatomic) CGColorSpaceRef srgb;
@end

@implementation OPCameraDelegate
- (instancetype)initWithOwner:(OPCameraSession *)owner callback:(opbc_frame_callback)callback context:(void *)context {
    self = [super init];
    if (self) {
        _owner = owner;
        _callback = callback;
        _context = context;
        _srgb = CGColorSpaceCreateWithName(kCGColorSpaceSRGB);
        _renderer = [CIContext contextWithOptions:@{kCIContextWorkingColorSpace:(__bridge id)_srgb,
            kCIContextOutputColorSpace:(__bridge id)_srgb, kCIContextCacheIntermediates:@NO}];
        NSDictionary *attributes = @{(__bridge NSString *)kCVPixelBufferPixelFormatTypeKey:@(kCVPixelFormatType_32BGRA),
            (__bridge NSString *)kCVPixelBufferWidthKey:@(owner.width),
            (__bridge NSString *)kCVPixelBufferHeightKey:@(owner.height),
            (__bridge NSString *)kCVPixelBufferIOSurfacePropertiesKey:@{}};
        CVPixelBufferPoolCreate(kCFAllocatorDefault, NULL, (__bridge CFDictionaryRef)attributes, &_pool);
    }
    return self;
}

- (void)dealloc {
    if (_pool) CVPixelBufferPoolRelease(_pool);
    if (_srgb) CGColorSpaceRelease(_srgb);
}

- (void)captureOutput:(AVCaptureOutput *)output didOutputSampleBuffer:(CMSampleBufferRef)sample fromConnection:(AVCaptureConnection *)connection {
    (void)output; (void)connection;
    @autoreleasepool {
        OPCameraSession *owner = self.owner;
        if (!owner || !owner.accepting) return;
        @try { [self publish:sample owner:owner]; }
        @catch (NSException *exception) {
            (void)exception;
            if (owner.accepting) self.callback(self.context, NULL, 0, 0, 0, 0, 0, OPBC_INTERNAL);
        }
    }
}

- (void)publish:(CMSampleBufferRef)sample owner:(OPCameraSession *)owner {
    CVPixelBufferRef source = CMSampleBufferGetImageBuffer(sample);
    if (!source || ![owner matches] || CVPixelBufferIsPlanar(source)
        || CVPixelBufferGetPixelFormatType(source) != kCVPixelFormatType_32BGRA
        || CVPixelBufferGetWidth(source) != (size_t)owner.width
        || CVPixelBufferGetHeight(source) != (size_t)owner.height
        || CVPixelBufferGetBytesPerRow(source) < (size_t)owner.width * 4) {
        self.callback(self.context, NULL, 0, 0, 0, 0, 0, OPBC_INVALID_FRAME);
        return;
    }
    CVPixelBufferRef destination = NULL;
    if (!self.pool || !self.renderer || CVPixelBufferPoolCreatePixelBuffer(kCFAllocatorDefault, self.pool, &destination) != kCVReturnSuccess) {
        self.callback(self.context, NULL, 0, 0, 0, 0, 0, OPBC_INTERNAL);
        return;
    }
    @try {
        CIImage *image = [CIImage imageWithCVPixelBuffer:source];
        if (!image.colorSpace) image = [CIImage imageWithCVPixelBuffer:source options:@{kCIImageColorSpace:(__bridge id)self.srgb}];
        [self.renderer render:image toCVPixelBuffer:destination bounds:CGRectMake(0, 0, owner.width, owner.height) colorSpace:self.srgb];
        [self deliver:destination sample:sample owner:owner];
    } @finally {
        CVPixelBufferRelease(destination);
    }
}

- (void)deliver:(CVPixelBufferRef)pixels sample:(CMSampleBufferRef)sample owner:(OPCameraSession *)owner {
    if (CVPixelBufferLockBaseAddress(pixels, 0) != kCVReturnSuccess) {
        self.callback(self.context, NULL, 0, 0, 0, 0, 0, OPBC_INVALID_FRAME);
        return;
    }
    size_t stride = CVPixelBufferGetBytesPerRow(pixels);
    size_t height = CVPixelBufferGetHeight(pixels);
    void *base = CVPixelBufferGetBaseAddress(pixels);
    @try {
        CMTime time = CMSampleBufferGetPresentationTimeStamp(sample);
        int64_t stamp = CMTIME_IS_NUMERIC(time) ? CMTimeConvertScale(time, NSEC_PER_SEC, kCMTimeRoundingMethod_Default).value : 0;
        if (owner.accepting && base && height == (size_t)owner.height && stride >= (size_t)owner.width * 4)
            self.callback(self.context, base, stride * height, owner.width, owner.height, stride, stamp, OPBC_OK);
    } @finally {
        if (base) memset(base, 0, stride * height);
        CVPixelBufferUnlockBaseAddress(pixels, 0);
    }
}
@end
