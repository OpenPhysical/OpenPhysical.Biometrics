# SPDX-License-Identifier: GPL-2.0-or-later
cmake_minimum_required(VERSION 3.20)
project(ScriptedLibUsbFixture LANGUAGES C)
add_library(scripted_libusb SHARED scripted-libusb.c)
set_target_properties(scripted_libusb PROPERTIES WINDOWS_EXPORT_ALL_SYMBOLS ON)
