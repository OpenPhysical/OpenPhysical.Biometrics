// SPDX-License-Identifier: GPL-2.0-or-later
#import <AVFoundation/AVFoundation.h>
#import "OpenPhysicalCamera.h"

NSArray<AVCaptureDevice *> *OPBCDevices(void);
NSArray<NSDictionary *> *OPBCModes(AVCaptureDevice *device);
AVCaptureDeviceFormat *OPBCFormat(AVCaptureDevice *device, int width, int height, CMTime duration);
BOOL OPBCFormatMatches(AVCaptureDevice *device, int width, int height, CMTime duration);
int OPBCPermission(void);
