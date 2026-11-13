// SPDX-License-Identifier: GPL-2.0-or-later
#import "OPCameraSession.h"
#import "OPCameraDelegate.h"
#import "OPCameraDevices.h"

static const void *OPBCSessionQueue = &OPBCSessionQueue;
static const void *OPBCOutputQueue = &OPBCOutputQueue;

@interface OPCameraSession ()
@property(nonatomic) AVCaptureSession *session;
@property(nonatomic) AVCaptureDeviceInput *input;
@property(nonatomic) AVCaptureVideoDataOutput *output;
@property(nonatomic) OPCameraDelegate *handler;
@property(nonatomic) dispatch_queue_t controlQueue;
@property(nonatomic) dispatch_queue_t outputQueue;
@property(nonatomic, readwrite) AVCaptureDevice *device;
@property(nonatomic, readwrite) int width;
@property(nonatomic, readwrite) int height;
@property(nonatomic, readwrite) CMTime duration;
@property(atomic, readwrite) BOOL accepting;
@end

@implementation OPCameraSession
- (instancetype)init {
    self = [super init];
    if (self) {
        _controlQueue = dispatch_queue_create("org.openphysical.camera.session", DISPATCH_QUEUE_SERIAL);
        _outputQueue = dispatch_queue_create("org.openphysical.camera.frames", DISPATCH_QUEUE_SERIAL);
        dispatch_queue_set_specific(_controlQueue, OPBCSessionQueue, (__bridge void *)self, NULL);
        dispatch_queue_set_specific(_outputQueue, OPBCOutputQueue, (__bridge void *)self, NULL);
    }
    return self;
}

- (int)control:(int (^)(void))action {
    __block int status = OPBC_INTERNAL;
    void (^guarded)(void) = ^{
        @autoreleasepool {
            @try { status = action(); }
            @catch (NSException *exception) { (void)exception; status = OPBC_INTERNAL; }
        }
    };
    if (dispatch_get_specific(OPBCSessionQueue) == (__bridge void *)self) guarded();
    else dispatch_sync(self.controlQueue, guarded);
    return status;
}

- (int)configure:(AVCaptureDevice *)device width:(int)width height:(int)height duration:(CMTime)duration {
    self.device = device;
    self.width = width;
    self.height = height;
    self.duration = duration;
    return [self control:^int { return [self configureLocked]; }];
}

- (int)configureLocked {
    AVCaptureDeviceFormat *format = OPBCFormat(self.device, self.width, self.height, self.duration);
    if (!format) return OPBC_UNSUPPORTED_MODE;
    NSError *error = nil;
    self.input = [AVCaptureDeviceInput deviceInputWithDevice:self.device error:&error];
    if (!self.input) return OPBC_UNAVAILABLE;
    self.session = [AVCaptureSession new];
    self.output = [AVCaptureVideoDataOutput new];
    self.output.alwaysDiscardsLateVideoFrames = YES;
    [self.session beginConfiguration];
    int status = OPBC_INTERNAL;
    @try {
        if (![self.session canAddInput:self.input] || ![self.session canAddOutput:self.output]) return OPBC_UNAVAILABLE;
        [self.session addInput:self.input];
        [self.session addOutput:self.output];
        status = [self configureFormat:format];
    } @finally {
        [self.session commitConfiguration];
    }
    return status == OPBC_OK && ![self matches] ? OPBC_UNSUPPORTED_MODE : status;
}

- (int)configureFormat:(AVCaptureDeviceFormat *)format {
    NSError *error = nil;
    if (![self.device lockForConfiguration:&error]) return OPBC_UNAVAILABLE;
    @try {
        self.device.activeFormat = format;
        self.device.activeVideoMinFrameDuration = self.duration;
        self.device.activeVideoMaxFrameDuration = self.duration;
        self.device.activeColorSpace = AVCaptureColorSpace_sRGB;
    } @finally {
        [self.device unlockForConfiguration];
    }
    if (![self.output.availableVideoCVPixelFormatTypes containsObject:@(kCVPixelFormatType_32BGRA)]) return OPBC_UNSUPPORTED_MODE;
    self.output.videoSettings = @{(__bridge NSString *)kCVPixelBufferPixelFormatTypeKey:@(kCVPixelFormatType_32BGRA)};
    AVCaptureConnection *connection = [self.output connectionWithMediaType:AVMediaTypeVideo];
    if (connection.isVideoMirroringSupported) {
        connection.automaticallyAdjustsVideoMirroring = NO;
        connection.videoMirrored = NO;
    }
    return OPBC_OK;
}

- (BOOL)matches {
    return OPBCFormatMatches(self.device, self.width, self.height, self.duration);
}

- (int)start:(opbc_frame_callback)callback context:(void *)context {
    if (!callback) return OPBC_INTERNAL;
    return [self control:^int {
        if (!self.session || self.accepting) return OPBC_CLOSED;
        self.handler = [[OPCameraDelegate alloc] initWithOwner:self callback:callback context:context];
        [self.output setSampleBufferDelegate:self.handler queue:self.outputQueue];
        self.accepting = YES;
        [self.session startRunning];
        if (!self.session.isRunning || ![self matches]) {
            self.accepting = NO;
            [self.output setSampleBufferDelegate:nil queue:NULL];
            [self.session stopRunning];
            return OPBC_UNSUPPORTED_MODE;
        }
        return OPBC_OK;
    }];
}

- (int)stop {
    self.accepting = NO;
    // Never form callback -> control -> callback cycles; managed callers stop off the callback thread.
    if (dispatch_get_specific(OPBCOutputQueue) == (__bridge void *)self) {
        dispatch_async(self.controlQueue, ^{ [self stop]; });
        return OPBC_INTERNAL;
    }
    int status = [self control:^int {
        [self.output setSampleBufferDelegate:nil queue:NULL];
        if (self.session.isRunning) [self.session stopRunning];
        return OPBC_OK;
    }];
    dispatch_sync(self.outputQueue, ^{});
    self.handler = nil;
    return status;
}
@end
