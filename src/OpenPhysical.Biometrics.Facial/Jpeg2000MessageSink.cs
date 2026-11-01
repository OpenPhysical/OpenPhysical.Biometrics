// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CoreJ2K.j2k.util;

// The CLI owns its diagnostic output and reports stable status codes. CoreJ2K requires
// an IMsgLogger implementation, so these members deliberately discard its messages.
internal sealed class Jpeg2000MessageSink : IMsgLogger
{
    public void printmsg(int severity, string message) { }

    public void println(string message, int firstIndent, int indent) { }

    public void flush() { }
}
