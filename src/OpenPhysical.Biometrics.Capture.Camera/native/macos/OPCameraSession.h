// SPDX-License-Identifier: GPL-2.0-or-later
#import <AVFoundation/AVFoundation.h>
#import "OpenPhysicalCamera.h"

@interface OPCameraSession : NSObject
@property(nonatomic, readonly) AVCaptureDevice *device;
@property(nonatomic, readonly) int width;
@property(nonatomic, readonly) int height;
@property(nonatomic, readonly) CMTime duration;
@property(atomic, readonly) BOOL accepting;
- (int)configure:(AVCaptureDevice *)device width:(int)width height:(int)height duration:(CMTime)duration;
- (int)start:(opbc_frame_callback)callback context:(void *)context;
- (int)stop;
- (BOOL)matches;
@end
