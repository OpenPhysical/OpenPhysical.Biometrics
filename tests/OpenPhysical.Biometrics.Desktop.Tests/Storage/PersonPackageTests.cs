// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using AwesomeAssertions;
using CSharpFunctionalExtensions.AwesomeAssertions;
using NUnit.Framework;

[TestFixture]
internal sealed class PersonPackageTests
{
    [Test]
    public async Task SavedSessionRetainsTheUserProvidedNameAndOptionalIdentifier()
    {
        var capture = ProcessingStorageFixtures.Capture();
        var record = UnsignedTemplateService.Create([capture], new TemplateMetadata(1, 0, 0)).Value;
        var person = CapturePerson.Create("  Lab alias  ", "  tester-17  ").Value;
        var folder = Path.Combine(
            Path.GetTempPath(),
            "biometrics-person-" + Guid.NewGuid().ToString("N")
        );
        var store = new TemplateStorage();
        var saved = await store.SaveSessionAsync(
            record,
            [capture],
            person,
            folder,
            CancellationToken.None
        );
        saved.Should().Succeed();
        var loaded = await store.LoadAsync(saved.Value, CancellationToken.None);
        loaded.Should().Succeed();
        loaded
            .Value.Subjects.Should()
            .ContainSingle()
            .Which.Should()
            .Be(new CapturePerson("Lab alias", "tester-17"));
        Directory.Delete(folder, true);
    }

    [Test]
    public void NameOrLabelIsRequiredAndIdentifiersStayOptional()
    {
        CapturePerson.Create("", "").Should().Fail().Which.Code.Should().Be("Session.Person");
        CapturePerson.Create("alias", "").Should().Succeed();
        CapturePerson.Create("line\nbreak", "").Should().Fail();
        CapturePerson.Create(new string('a', 129), "").Should().Fail();
    }
}
