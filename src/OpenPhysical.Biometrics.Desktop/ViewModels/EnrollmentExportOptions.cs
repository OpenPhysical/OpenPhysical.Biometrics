// SPDX-License-Identifier: GPL-2.0-or-later
using CommunityToolkit.Mvvm.ComponentModel;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Encoding identifiers and destination choices shared by the capture workspace.</summary>
public abstract class EnrollmentExportOptions : ObservableObject
{
    private string owner = EnrollmentWorkspaceReset.ProductOwner;
    private string productType = EnrollmentWorkspaceReset.Unreported;
    private string equipment = EnrollmentWorkspaceReset.Unreported;
    private string folder = TemplateStorage.DefaultFolder;
    private bool includeDiagnostics;

    /// <summary>Gets or sets whether diagnostic contact planes are explicitly saved.</summary>
    public bool IncludeDiagnostics
    {
        get => includeDiagnostics;
        set => SetProperty(ref includeDiagnostics, value);
    }

    /// <summary>Gets or sets the CBEFF product owner in decimal or hexadecimal.</summary>
    public string ProductOwner
    {
        get => owner;
        set
        {
            if (SetProperty(ref owner, value))
                OptionsChanged();
        }
    }

    /// <summary>Gets or sets the encoding product type.</summary>
    public string ProductType
    {
        get => productType;
        set
        {
            if (SetProperty(ref productType, value))
                OptionsChanged();
        }
    }

    /// <summary>Gets or sets the capture equipment identifier.</summary>
    public string EquipmentId
    {
        get => equipment;
        set
        {
            if (SetProperty(ref equipment, value))
                OptionsChanged();
        }
    }

    /// <summary>Gets or sets the persistent package destination.</summary>
    public string Folder
    {
        get => folder;
        set => SetProperty(ref folder, value);
    }

    /// <summary>Parses the current standard encoding identifiers.</summary>
    /// <returns>Validated encoding metadata or a typed field failure.</returns>
    protected Result<TemplateMetadata, DesktopError> Metadata() =>
        TemplateMetadataParser.Parse(ProductOwner, ProductType, EquipmentId);

    /// <summary>Updates readiness when an encoding choice changes.</summary>
    /// <returns>The updated workspace readiness.</returns>
    protected abstract UnitResult<DesktopError> OptionsChanged();
}
