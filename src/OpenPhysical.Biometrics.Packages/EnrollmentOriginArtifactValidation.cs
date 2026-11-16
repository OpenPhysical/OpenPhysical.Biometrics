// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Packages;

using Org.BouncyCastle.Asn1;

internal static class EnrollmentOriginArtifactValidation
{
    internal static bool ValidArtifactEntries(Asn1Sequence artifacts)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < artifacts.Count; i++)
        {
            var item = Asn1Sequence.GetInstance(artifacts[i]);
            if (!ValidArtifactEntry(item, i + 1, names))
                return false;
        }
        return true;
    }

    private static bool ValidArtifactEntry(
        Asn1Sequence item,
        int expectedIndex,
        HashSet<string> names
    )
    {
        if (
            item.Count != 6
            || !EnrollmentOriginArtifactCodec.Integer(item[0], expectedIndex)
            || Asn1OctetString.GetInstance(item[5]).GetOctets().Length != 32
        )
            return false;
        return ValidArtifactValues(item, names);
    }

    private static bool ValidArtifactValues(Asn1Sequence item, HashSet<string> names)
    {
        var name = EnrollmentOriginArtifactCodec.Text(item[1]);
        var role = EnrollmentOriginArtifactCodec.Text(item[2]);
        var mediaType = EnrollmentOriginArtifactCodec.Text(item[3]);
        var validLength = EnrollmentDerIntegers.TryBoundedInt(item[4], 0, 67_108_864, out _);
        return PackageNames.Valid(name)
            && names.Add(name)
            && EnrollmentOriginArtifactCodec.ValidText(role, 128)
            && EnrollmentOriginArtifactCodec.ValidText(mediaType, 128)
            && validLength;
    }
}
