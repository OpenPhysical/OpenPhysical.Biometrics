// SPDX-License-Identifier: GPL-2.0-or-later
#import <AVFoundation/AVFoundation.h>
#import "OpenPhysicalCamera.h"
@class OPCameraSession;

@interface OPCameraDelegate : NSObject <AVCaptureVideoDataOutputSampleBufferDelegate>
- (instancetype)initWithOwner:(OPCameraSession *)owner callback:(opbc_frame_callback)callback context:(void *)context;
@end
