namespace OpenPhysical.Biometrics.Core.Domain;

using System;
using CSharpFunctionalExtensions;

/// <summary>
/// Represents a Federal Agency Smart Credential Number (FASC-N) as defined in FIPS 201.
/// </summary>
public sealed record FascN
{
    /// <summary>
    /// Organizational category enumeration.
    /// </summary>
    public enum OrganizationalCategory
    {
        /// <summary>The category or affiliation was not specified.</summary>
        Unspecified = 0,

        /// <summary>A federal government agency.</summary>
        FederalGovernmentAgency = 1,

        /// <summary>A state government agency.</summary>
        StateGovernmentAgency = 2,

        /// <summary>A commercial organization.</summary>
        CommercialEnterprise = 3,

        /// <summary>A government outside the issuing country.</summary>
        ForeignGovernment = 4,
    }

    /// <summary>
    /// Person organization affiliation enumeration.
    /// </summary>
    public enum PersonOrganizationAffiliation
    {
        /// <summary>The category or affiliation was not specified.</summary>
        Unspecified = 0,

        /// <summary>An employee of the identified organization.</summary>
        Employee = 1,

        /// <summary>A member of the civil service.</summary>
        CivilService = 2,

        /// <summary>A member of the executive staff.</summary>
        ExecutiveStaff = 3,

        /// <summary>A member of a uniformed service.</summary>
        UniformedService = 4,

        /// <summary>A contractor supporting the identified organization.</summary>
        Contractor = 5,

        /// <summary>An affiliate of the identified organization.</summary>
        OrganizationalAffiliate = 6,

        /// <summary>A beneficiary associated with the identified organization.</summary>
        OrganizationalBeneficiary = 7,
    }

    /// <summary>
    /// Gets the agency code (4 digits).
    /// </summary>
    public int AgencyCode { get; init; }

    /// <summary>
    /// Gets the system code (4 digits).
    /// </summary>
    public int SystemCode { get; init; }

    /// <summary>
    /// Gets the credential number (6 digits).
    /// </summary>
    public int CredentialNumber { get; init; }

    /// <summary>
    /// Gets the credential series (1 digit).
    /// </summary>
    public int CredentialSeries { get; init; }

    /// <summary>
    /// Gets the individual credential issue (1 digit).
    /// </summary>
    public int IndividualCredentialIssue { get; init; }

    /// <summary>
    /// Gets the person identifier (10 digits).
    /// </summary>
    public long PersonIdentifier { get; init; }

    /// <summary>
    /// Gets the organizational category.
    /// </summary>
    public OrganizationalCategory Category { get; init; }

    /// <summary>
    /// Gets the organizational identifier (4 digits).
    /// </summary>
    public int OrganizationalIdentifier { get; init; }

    /// <summary>
    /// Gets the person organization affiliation.
    /// </summary>
    public PersonOrganizationAffiliation Affiliation { get; init; }

    /// <summary>
    /// Creates a new FASC-N with validation.
    /// </summary>
    public static Result<FascN> Create(
        int agencyCode,
        int systemCode,
        int credentialNumber,
        int credentialSeries = 0,
        int individualCredentialIssue = 0,
        long personIdentifier = 0,
        OrganizationalCategory category = OrganizationalCategory.Unspecified,
        int organizationalIdentifier = 0,
        PersonOrganizationAffiliation affiliation = PersonOrganizationAffiliation.Unspecified
    )
    {
        return Result
            .Success()
            .Ensure(() => agencyCode is >= 0 and <= 9999, "Agency code must be between 0 and 9999")
            .Ensure(() => systemCode is >= 0 and <= 9999, "System code must be between 0 and 9999")
            .Ensure(
                () => credentialNumber is >= 0 and <= 999999,
                "Credential number must be between 0 and 999999"
            )
            .Ensure(
                () => credentialSeries is >= 0 and <= 9,
                "Credential series must be between 0 and 9"
            )
            .Ensure(
                () => individualCredentialIssue is >= 0 and <= 9,
                "Individual credential issue must be between 0 and 9"
            )
            .Ensure(
                () => personIdentifier is >= 0 and <= 9999999999L,
                "Person identifier must be between 0 and 9999999999"
            )
            .Ensure(
                () => organizationalIdentifier is >= 0 and <= 9999,
                "Organizational identifier must be between 0 and 9999"
            )
            .Map(() =>
                new FascN
                {
                    AgencyCode = agencyCode,
                    SystemCode = systemCode,
                    CredentialNumber = credentialNumber,
                    CredentialSeries = credentialSeries,
                    IndividualCredentialIssue = individualCredentialIssue,
                    PersonIdentifier = personIdentifier,
                    Category = category,
                    OrganizationalIdentifier = organizationalIdentifier,
                    Affiliation = affiliation,
                }
            );
    }

    /// <summary>
    /// Returns a human-readable representation of the FASC-N.
    /// </summary>
    public override string ToString()
    {
        return $"Agency:{AgencyCode:D4} System:{SystemCode:D4} Cred:{CredentialNumber:D6}-{CredentialSeries}-{IndividualCredentialIssue} "
            + $"Person:{PersonIdentifier:D10} Org:{(int)Category}-{OrganizationalIdentifier:D4}-{(int)Affiliation}";
    }

    /// <summary>
    /// Gets the organizational category description.
    /// </summary>
    public string GetCategoryDescription() =>
        Category switch
        {
            OrganizationalCategory.FederalGovernmentAgency => "Federal Government Agency",
            OrganizationalCategory.StateGovernmentAgency => "State Government Agency",
            OrganizationalCategory.CommercialEnterprise => "Commercial Enterprise",
            OrganizationalCategory.ForeignGovernment => "Foreign Government",
            _ => "Unspecified",
        };

    /// <summary>
    /// Gets the person organization affiliation description.
    /// </summary>
    public string GetAffiliationDescription() =>
        Affiliation switch
        {
            PersonOrganizationAffiliation.Employee => "Employee",
            PersonOrganizationAffiliation.CivilService => "Civil Service",
            PersonOrganizationAffiliation.ExecutiveStaff => "Executive Staff",
            PersonOrganizationAffiliation.UniformedService => "Uniformed Service",
            PersonOrganizationAffiliation.Contractor => "Contractor",
            PersonOrganizationAffiliation.OrganizationalAffiliate => "Organizational Affiliate",
            PersonOrganizationAffiliation.OrganizationalBeneficiary => "Organizational Beneficiary",
            _ => "Unspecified",
        };
}
