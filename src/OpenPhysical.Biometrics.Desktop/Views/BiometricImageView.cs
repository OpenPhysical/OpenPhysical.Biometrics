// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Desktop;

/// <summary>Fingerprint image workspace with overlays and accessible display controls.</summary>
public sealed class BiometricImageView : UserControl
{
    /// <summary>Defines the displayed image and canonical features.</summary>
    public static readonly StyledProperty<Maybe<FingerprintScene>> SceneProperty =
        AvaloniaProperty.Register<BiometricImageView, Maybe<FingerprintScene>>(nameof(Scene));

    /// <summary>Defines the empty-image instruction.</summary>
    public static readonly StyledProperty<string> EmptyTextProperty = AvaloniaProperty.Register<
        BiometricImageView,
        string
    >(nameof(EmptyText), string.Empty);

    /// <summary>Defines display-only pixel inversion.</summary>
    public static readonly StyledProperty<bool> InvertProperty = AvaloniaProperty.Register<
        BiometricImageView,
        bool
    >(nameof(Invert));

    /// <summary>Defines feature overlay visibility.</summary>
    public static readonly StyledProperty<bool> ShowFeaturesProperty = AvaloniaProperty.Register<
        BiometricImageView,
        bool
    >(nameof(ShowFeatures), true);

    /// <summary>Defines finger area outline visibility.</summary>
    public static readonly StyledProperty<bool> ShowAreaProperty = AvaloniaProperty.Register<
        BiometricImageView,
        bool
    >(nameof(ShowArea), true);

    /// <summary>Defines the feature overlay label.</summary>
    public static readonly StyledProperty<string> FeatureLabelProperty = AvaloniaProperty.Register<
        BiometricImageView,
        string
    >(nameof(FeatureLabel), "Features");

    /// <summary>Creates the image workspace and display controls.</summary>
    public BiometricImageView() => AvaloniaXamlLoader.Load(this);

    /// <summary>Gets or sets displayed immutable image and canonical features.</summary>
    public Maybe<FingerprintScene> Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <summary>Gets or sets instruction shown when no image is available.</summary>
    public string EmptyText
    {
        get => GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    /// <summary>Gets or sets display-only pixel inversion.</summary>
    public bool Invert
    {
        get => GetValue(InvertProperty);
        set => SetValue(InvertProperty, value);
    }

    /// <summary>Gets or sets feature overlay visibility.</summary>
    public bool ShowFeatures
    {
        get => GetValue(ShowFeaturesProperty);
        set => SetValue(ShowFeaturesProperty, value);
    }

    /// <summary>Gets or sets finger area outline visibility.</summary>
    public bool ShowArea
    {
        get => GetValue(ShowAreaProperty);
        set => SetValue(ShowAreaProperty, value);
    }

    /// <summary>Gets or sets operator label for the feature overlay.</summary>
    public string FeatureLabel
    {
        get => GetValue(FeatureLabelProperty);
        set => SetValue(FeatureLabelProperty, value);
    }
}
