// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CSharpFunctionalExtensions;
using OpenPhysical.Middleware.Domain.Piv;
using OpenPhysical.Workstation.Application.Devices;
using OpenPhysical.Workstation.Infrastructure.Verification;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Operator selections, containing no cached authority or proof.</summary>
public sealed class StudioVerificationCardInput : ObservableObject
{
    private StudioVerificationState state = StudioVerificationState.Empty;
    private int choiceIndex = -1;
    private int readerIndex = -1;
    private int scannerIndex = -1;
    private int marsecLevel = 1;
    private string pin = string.Empty;
    private bool contactless;

    /// <summary>Checks currently allowed by the authenticated settings.</summary>
    public ImmutableArray<StudioVerificationChoice> Choices => state.Choices;

    /// <summary>Current card readers.</summary>
    public ImmutableArray<WorkstationReader> Readers => state.Readers;

    /// <summary>Current fingerprint scanners.</summary>
    public ImmutableArray<WorkstationCaptureDevice> Scanners => state.Scanners;

    /// <summary>Selected permitted check index.</summary>
    public int ChoiceIndex
    {
        get => choiceIndex;
        set
        {
            if (SetProperty(ref choiceIndex, value))
                Changed();
        }
    }

    /// <summary>Selected current reader index.</summary>
    public int ReaderIndex
    {
        get => readerIndex;
        set => SetProperty(ref readerIndex, value);
    }

    /// <summary>Selected current scanner index.</summary>
    public int ScannerIndex
    {
        get => scannerIndex;
        set => SetProperty(ref scannerIndex, value);
    }

    /// <summary>Explicit TWIC maritime security level.</summary>
    public int MarsecLevel
    {
        get => marsecLevel;
        set => SetProperty(ref marsecLevel, value);
    }

    /// <summary>Whether the operator selected a contactless physical interface.</summary>
    public bool Contactless
    {
        get => contactless;
        set => SetProperty(ref contactless, value);
    }

    /// <summary>Transient holder PIN, cleared immediately when submitted or paused.</summary>
    public string Pin
    {
        get => pin;
        set => SetProperty(ref pin, value);
    }

    /// <summary>Selected authenticated ceremony, if available.</summary>
    public Maybe<StudioVerificationChoice> SelectedChoice => At(Choices, ChoiceIndex);

    /// <summary>Selected opaque reader identity.</summary>
    public Maybe<WorkstationReader> SelectedReader => At(Readers, ReaderIndex);

    /// <summary>Selected opaque capture identity.</summary>
    public Maybe<WorkstationCaptureDevice> SelectedScanner => At(Scanners, ScannerIndex);

    /// <summary>Whether this check requires holder PIN input.</summary>
    public bool NeedsPin => SelectedChoice.Match(choice => choice.NeedsPin, () => false);

    /// <summary>Whether this check requires scanner selection.</summary>
    public bool NeedsScanner => SelectedChoice.Match(choice => choice.NeedsScanner, () => false);

    /// <summary>Whether TWIC-specific settings are needed.</summary>
    public bool IsTwic =>
        SelectedChoice.Match(
            choice => choice.Kind == StudioVerificationKind.TwicFingerprint,
            () => false
        );

    internal UnitResult<DesktopError> Load(StudioVerificationState current)
    {
        state = current;
        Pin = string.Empty;
        foreach (var name in new[] { nameof(Choices), nameof(Readers), nameof(Scanners) })
            OnPropertyChanged(name);
        ChoiceIndex = Choices.Length == 1 ? 0 : -1;
        ReaderIndex = Readers.Length == 1 ? 0 : -1;
        ScannerIndex = Scanners.Length == 1 ? 0 : -1;
        Changed();
        return UnitResult.Success<DesktopError>();
    }

    internal Result<StudioVerificationSelection, DesktopError> Select(
        StudioVerificationMaterial material
    )
    {
        if (
            !state.Ready
            || SelectedChoice.HasNoValue
            || SelectedReader.HasNoValue
            || NeedsScanner && SelectedScanner.HasNoValue
            || MarsecLevel is < 1 or > 3
        )
            return new DesktopError(
                "Verification.Selection",
                "Choose an available card check and reader. Fingerprint checks also need a scanner."
            );
        return new StudioVerificationSelection(
            SelectedReader.Value.ReaderId,
            Contactless ? PivInterfaceType.Contactless : PivInterfaceType.Contact,
            SelectedChoice.Value,
            SelectedScanner.Match(scanner => scanner.DeviceId, () => string.Empty),
            MarsecLevel,
            material.Issuers,
            material.Signers
        );
    }

    private UnitResult<DesktopError> Changed()
    {
        Pin = string.Empty;
        foreach (
            var name in new[]
            {
                nameof(SelectedChoice),
                nameof(NeedsPin),
                nameof(NeedsScanner),
                nameof(IsTwic),
            }
        )
            OnPropertyChanged(name);
        return UnitResult.Success<DesktopError>();
    }

    private static Maybe<T> At<T>(ImmutableArray<T> values, int index)
        where T : class => index >= 0 && index < values.Length ? values[index] : Maybe<T>.None;
}
