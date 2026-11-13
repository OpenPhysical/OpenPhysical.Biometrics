// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop;

using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Piv;

/// <summary>Read-only inspection that owns its display copies and reuses retained features.</summary>
public sealed class PackageReviewViewModel : ObservableObject, IDisposable
{
    private Maybe<PackageReviewDocument> document;
    private ImmutableArray<FacePreviewScene> portraits = [];
    private PackageFaceChoice selectedFace = new(-1, 0, 0);
    private Maybe<FacePreviewScene> faceScene;
    private Maybe<FingerprintScene> fingerprintScene;

    private PackageReviewViewModel(PackageReviewDocument value)
    {
        document = value;
        Label = value.Label;
        ExternalId = value.ExternalId.GetValueOrDefault(string.Empty);
        FingerSelection = new(SelectFingerprint);
        FaceDetails = Profile("Face", value.FaceProfileFindings, value.Face.HasValue);
        FingerprintDetails = Profile(
            "Fingerprints",
            value.FingerprintProfileFindings,
            value.Fingerprints.HasValue
        );
    }

    /// <summary>Gets the shared person label.</summary>
    public string Label { get; private set; }

    /// <summary>Gets the optional person identifier.</summary>
    public string ExternalId { get; private set; }

    /// <summary>Gets whether the person identifier is present.</summary>
    public bool HasExternalId => !string.IsNullOrEmpty(ExternalId);

    /// <summary>Gets whether a facial record is available.</summary>
    public bool HasFace => FaceChoices.Length > 0;

    /// <summary>Gets whether selecting another portrait is useful.</summary>
    public bool HasMultipleFaces => FaceChoices.Length > 1;

    /// <summary>Gets whether fingerprint views are available.</summary>
    public bool HasFingerprints => FingerSelection.Views.Length > 0;

    /// <summary>Gets the first available modality tab.</summary>
    public int InitialTabIndex => HasFace ? 0 : 1;

    /// <summary>Gets available image blocks without changing their pixels.</summary>
    public ImmutableArray<PackageFaceChoice> FaceChoices { get; private set; } = [];

    /// <summary>Gets actual retained fingerprint views.</summary>
    public ReferenceSelectionViewModel FingerSelection { get; }

    /// <summary>Gets facial metadata findings separately from file checks.</summary>
    public string FaceDetails { get; }

    /// <summary>Gets fingerprint metadata findings separately from file checks.</summary>
    public string FingerprintDetails { get; }

    /// <summary>Gets or sets the facial image block being inspected.</summary>
    public PackageFaceChoice SelectedFace
    {
        get => selectedFace;
        set
        {
            if (
                value is null
                || !FaceChoices.Contains(value)
                || !SetProperty(ref selectedFace, value)
            )
                return;
            FaceScene = portraits[value.Index];
        }
    }

    /// <summary>Gets the selected facial display copy without review landmarks.</summary>
    public Maybe<FacePreviewScene> FaceScene
    {
        get => faceScene;
        private set => SetProperty(ref faceScene, value);
    }

    /// <summary>Gets the selected fingerprint image and its exact saved features.</summary>
    public Maybe<FingerprintScene> FingerprintScene
    {
        get => fingerprintScene;
        private set => SetProperty(ref fingerprintScene, value);
    }

    /// <summary>Decodes display images without inference, assessment, or re-encoding.</summary>
    /// <param name="document">Validated objects and companions.</param>
    /// <returns>An owned read-only model, or a display/record failure.</returns>
    public static Result<PackageReviewViewModel, DesktopError> Create(
        PackageReviewDocument document
    )
    {
        var model = new PackageReviewViewModel(document);
        var initialized = model.Initialize(document);
        if (initialized.IsSuccess)
            return model;
        model.Dispose();
        return initialized.Error;
    }

    private UnitResult<DesktopError> Initialize(PackageReviewDocument value)
    {
        if (value.Face.HasValue)
        {
            var prepared = PreparePortraits(value);
            if (prepared.IsFailure)
                return prepared.Error;
        }
        if (value.Fingerprints.HasNoValue)
            return UnitResult.Success<DesktopError>();
        FingerSelection.Replace(value.Fingerprints.Value.Record);
        return SelectFingerprint(FingerSelection.SelectedView);
    }

    private UnitResult<DesktopError> PreparePortraits(PackageReviewDocument value)
    {
        var images = value.Face.Value.Images;
        if (
            images.Sum(image => (long)image.ImageInformation.Width * image.ImageInformation.Height)
            > 32 * 1024 * 1024
        )
            return new DesktopError(
                "Package.ImageLimit",
                "The portraits exceed the inspection pixel budget."
            );
        foreach (var image in images)
        {
            var scene = PackageReviewFaceScene.Create(image);
            if (scene.IsFailure)
                return scene.Error;
            portraits = portraits.Add(scene.Value);
        }
        FaceChoices = images
            .Select(
                (image, index) =>
                    new PackageFaceChoice(
                        index,
                        image.ImageInformation.Width,
                        image.ImageInformation.Height
                    )
            )
            .ToImmutableArray();
        SelectedFace = FaceChoices[0];
        return UnitResult.Success<DesktopError>();
    }

    private UnitResult<DesktopError> SelectFingerprint(ReferenceViewChoice choice)
    {
        if (document.HasNoValue || document.Value.Fingerprints.HasNoValue)
            return new DesktopError("Package.Closed", "This package inspection has closed.");
        if (!FingerSelection.Views.Contains(choice))
            return new DesktopError("Storage.View", "Select an actual saved finger view.");
        var result = SceneFactory.Document(document.Value.Fingerprints.Value, choice.Index);
        if (result.IsFailure)
            return result.Error;
        var previous = FingerprintScene;
        FingerprintScene = result.Value;
        if (previous.HasValue)
            previous.Value.Dispose();
        return UnitResult.Success<DesktopError>();
    }

    private static string Profile(
        string label,
        ImmutableArray<BiometricProfileDiagnostic> findings,
        bool present
    ) =>
        !present ? $"{label}: no object."
        : findings.IsEmpty ? $"{label} PIV metadata: no findings."
        : $"{label} PIV metadata:\n"
            + string.Join("\n", findings.Select(value => $"{value.Code}: {value.Field}"));

    /// <summary>Releases all image copies and person references.</summary>
    public void Dispose()
    {
        document = Maybe<PackageReviewDocument>.None;
        if (FingerprintScene.HasValue)
            FingerprintScene.Value.Dispose();
        FingerprintScene = Maybe<FingerprintScene>.None;
        FaceScene = Maybe<FacePreviewScene>.None;
        foreach (var portrait in portraits)
            portrait.Dispose();
        portraits = [];
        FaceChoices = [];
        FingerSelection.Forget();
        Label = ExternalId = string.Empty;
    }
}
