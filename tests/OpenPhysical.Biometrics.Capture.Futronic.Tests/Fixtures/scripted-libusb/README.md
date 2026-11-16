# Scripted libusb fixture

This hardware mock is built with CMake and a C compiler during the test build.
It exports the libusb ABI and is loaded through the production public provider's
explicit library path. Endpoint scripts cover partial timed-out input, quiet
timeout, disconnect, partial output, and impossible native byte counts.
It never enumerates or accesses real hardware. It contains no biometric data.
