// SPDX-License-Identifier: GPL-2.0-or-later
using OpenPhysical.Biometrics.Examples;

if (args is ["--sd04", var fixtureDirectory])
    return Sd04MatchingExample.Run(fixtureDirectory);

if (args.Length == 4)
    return FingerprintWorkflowExample.Run(args);

Console.WriteLine("Examples <first.gray8> <second.gray8> <width> <height>");
return 2;
