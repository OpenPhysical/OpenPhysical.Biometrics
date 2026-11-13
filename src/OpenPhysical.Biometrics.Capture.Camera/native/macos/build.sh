#!/bin/sh
# SPDX-License-Identifier: GPL-2.0-or-later
set -eu
if [ "$#" -ne 1 ]; then
    echo "usage: build.sh /absolute/path/libopenphysical_camera.dylib" >&2
    exit 2
fi
case "$1" in /*) ;; *) echo "output must be an absolute path" >&2; exit 2 ;; esac
script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
output_path=$1
mkdir -p "$(dirname -- "$output_path")"
sdk_path=$(xcrun --sdk macosx --show-sdk-path)
arch_flags=""
for camera_arch in ${OPBC_ARCHS:-arm64}; do
    case "$camera_arch" in arm64|x86_64) ;; *) echo "unsupported architecture" >&2; exit 2 ;; esac
    arch_flags="$arch_flags -arch $camera_arch"
done
# These flags intentionally fail on accidental iOS-only API use in the macOS bridge.
xcrun --sdk macosx clang $arch_flags -isysroot "$sdk_path" -mmacosx-version-min=13.0 \
    -fobjc-arc -fblocks -O2 -Wall -Wextra -Werror -fvisibility=hidden -dynamiclib \
    -framework Foundation -framework AVFoundation -framework CoreMedia \
    -framework CoreVideo -framework CoreImage -framework CoreGraphics \
    -install_name @rpath/libopenphysical_camera.dylib \
    "$script_dir/OpenPhysicalCamera.m" "$script_dir/OPCameraDevices.m" \
    "$script_dir/OPCameraSession.m" "$script_dir/OPCameraDelegate.m" \
    -o "$output_path"
