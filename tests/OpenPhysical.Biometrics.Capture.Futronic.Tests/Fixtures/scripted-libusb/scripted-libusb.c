// SPDX-License-Identifier: GPL-2.0-or-later
#include <stdint.h>
#include <string.h>

// A hardware boundary mock loaded through the public explicit-library-path provider.
static void *devices[] = {(void *)(uintptr_t)1, 0};
int libusb_init(void **context) { *context = devices[0]; return 0; }
void libusb_exit(void *context) { (void)context; }
intptr_t libusb_get_device_list(void *context, void ***list) {
    (void)context; *list = devices; return 1;
}
void libusb_free_device_list(void **list, int unref) { (void)list; (void)unref; }
int libusb_get_device_descriptor(void *device, unsigned char *descriptor) {
    (void)device;
    memset(descriptor, 0, 18);
    descriptor[0] = 18;
    descriptor[8] = 0x34; descriptor[9] = 0x12;
    descriptor[10] = 0x78; descriptor[11] = 0x56;
    return 0;
}
unsigned char libusb_get_bus_number(void *device) { (void)device; return 1; }
unsigned char libusb_get_device_address(void *device) { (void)device; return 1; }
int libusb_open(void *device, void **handle) { *handle = device; return 0; }
void libusb_close(void *handle) { (void)handle; }
int libusb_claim_interface(void *handle, int number) { (void)handle; (void)number; return 0; }
int libusb_release_interface(void *handle, int number) { (void)handle; (void)number; return 0; }
int libusb_bulk_transfer(void *handle, unsigned char endpoint, unsigned char *data,
                         int length, int *transferred, unsigned int timeout) {
    (void)handle; (void)timeout;
    *transferred = 0;
    switch (endpoint) {
        case 0x81:
            *transferred = length < 64 ? length : 64;
            memset(data, 0xA5, (size_t)*transferred);
            return -7;
        case 0x82: return -7;
        case 0x83: return -4;
        case 0x84: *transferred = -1; return 0;
        case 0x85: *transferred = length + 1; return 0;
        case 0x01: *transferred = 1; return -7;
        default:
            *transferred = length;
            memset(data, 0xA5, (size_t)length);
            return 0;
    }
}
