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
: "${OPBC_ARCHS:?set OPBC_ARCHS explicitly to arm64 or x86_64}"
case "$OPBC_ARCHS" in arm64|x86_64) ;; *) echo "OPBC_ARCHS must name exactly one supported architecture" >&2; exit 2 ;; esac
# These flags intentionally fail on accidental iOS-only API use in the macOS bridge.
xcrun --sdk macosx clang -arch "$OPBC_ARCHS" -isysroot "$sdk_path" -mmacosx-version-min=13.0 \
    -fobjc-arc -fblocks -O2 -Wall -Wextra -Werror -fvisibility=hidden -dynamiclib \
    -framework Foundation -framework AVFoundation -framework CoreMedia \
    -framework CoreVideo -framework CoreImage -framework CoreGraphics \
    -install_name @rpath/libopenphysical_camera.dylib \
    "$script_dir/OpenPhysicalCamera.m" "$script_dir/OPCameraDevices.m" \
    "$script_dir/OPCameraSession.m" "$script_dir/OPCameraDelegate.m" \
    -o "$output_path"
