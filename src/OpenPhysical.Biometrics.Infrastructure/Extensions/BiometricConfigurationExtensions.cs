using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenPhysical.Biometrics.Core.Configuration;
using JetBrains.Annotations;
using CSharpFunctionalExtensions;

namespace OpenPhysical.Biometrics.Infrastructure.Extensions;

/// <summary>
/// Extension methods for configuring BiometricConfiguration with Microsoft.Extensions.Configuration
/// </summary>
[PublicAPI]
public static class BiometricConfigurationExtensions
{
    /// <summary>
    /// Adds BiometricConfiguration to the service collection with default values
    /// </summary>
    public static IServiceCollection AddBiometricConfiguration(
        this IServiceCollection services)
    {
        return services.AddSingleton(BiometricConfiguration.Default);
    }

    /// <summary>
    /// Adds BiometricConfiguration to the service collection with custom configuration
    /// </summary>
    public static IServiceCollection AddBiometricConfiguration(
        this IServiceCollection services,
        Func<BiometricConfigurationBuilder, BiometricConfigurationBuilder> configure)
    {
        var result = configure(BiometricConfigurationBuilder.Create()).Build();
        
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Invalid biometric configuration: {result.Error}");
        }

        return services.AddSingleton(result.Value);
    }

    /// <summary>
    /// Adds BiometricConfiguration from IConfiguration (appsettings.json, environment variables, etc.)
    /// </summary>
    public static IServiceCollection AddBiometricConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Biometrics")
    {
        var config = LoadFromConfiguration(configuration, sectionName);
        return services.AddSingleton(config);
    }

    /// <summary>
    /// Adds BiometricConfiguration with Options pattern support
    /// </summary>
    public static IServiceCollection AddBiometricConfigurationWithOptions(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Biometrics")
    {
        services.Configure<BiometricConfigurationOptions>(
            configuration.GetSection(sectionName));

        services.AddSingleton<BiometricConfiguration>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<BiometricConfigurationOptions>>().Value;
            return BuildFromOptions(options);
        });

        return services;
    }

    /// <summary>
    /// Loads BiometricConfiguration from IConfiguration with fallback to defaults
    /// </summary>
    public static BiometricConfiguration LoadFromConfiguration(
        IConfiguration configuration,
        string sectionName = "Biometrics")
    {
        var section = configuration.GetSection(sectionName);
        
        if (!section.Exists())
        {
            return BiometricConfiguration.Default;
        }

        var builder = BiometricConfigurationBuilder.Create();

        // Load face detection configuration
        var faceDetectionSection = section.GetSection("FaceDetection");
        if (faceDetectionSection.Exists())
        {
            builder = builder.WithFaceDetection(config => config with
            {
                MinConfidence = faceDetectionSection.GetValue("MinConfidence", config.MinConfidence),
                StrictModeMinConfidence = faceDetectionSection.GetValue("StrictModeMinConfidence", config.StrictModeMinConfidence),
                ArchivalMinConfidence = faceDetectionSection.GetValue("ArchivalMinConfidence", config.ArchivalMinConfidence),
                RelaxedMinConfidence = faceDetectionSection.GetValue("RelaxedMinConfidence", config.RelaxedMinConfidence),
                RequireSingleFace = faceDetectionSection.GetValue("RequireSingleFace", config.RequireSingleFace),
                MaxRotationDegrees = faceDetectionSection.GetValue("MaxRotationDegrees", config.MaxRotationDegrees)
            });
        }

        // Load encoding configuration
        var encodingSection = section.GetSection("Encoding");
        if (encodingSection.Exists())
        {
            var jpeg2000Section = encodingSection.GetSection("Jpeg2000");
            var jpegSection = encodingSection.GetSection("Jpeg");

            builder = builder.WithEncoding(config => config with
            {
                DefaultFormat = encodingSection.GetValue("DefaultFormat", config.DefaultFormat),
                Jpeg2000 = jpeg2000Section.Exists() ? config.Jpeg2000 with
                {
                    DefaultRate = jpeg2000Section.GetValue("DefaultRate", config.Jpeg2000.DefaultRate),
                    HighQualityRate = jpeg2000Section.GetValue("HighQualityRate", config.Jpeg2000.HighQualityRate),
                    ArchivalRate = jpeg2000Section.GetValue("ArchivalRate", config.Jpeg2000.ArchivalRate),
                    DefaultRoiStartLevel = jpeg2000Section.GetValue("DefaultRoiStartLevel", config.Jpeg2000.DefaultRoiStartLevel),
                    ConservativeRoiStartLevel = jpeg2000Section.GetValue("ConservativeRoiStartLevel", config.Jpeg2000.ConservativeRoiStartLevel),
                    AggressiveRoiStartLevel = jpeg2000Section.GetValue("AggressiveRoiStartLevel", config.Jpeg2000.AggressiveRoiStartLevel),
                    EnableRoiByDefault = jpeg2000Section.GetValue("EnableRoiByDefault", config.Jpeg2000.EnableRoiByDefault),
                    AlignRoiByDefault = jpeg2000Section.GetValue("AlignRoiByDefault", config.Jpeg2000.AlignRoiByDefault)
                } : config.Jpeg2000,
                Jpeg = jpegSection.Exists() ? config.Jpeg with
                {
                    DefaultQuality = jpegSection.GetValue("DefaultQuality", config.Jpeg.DefaultQuality),
                    HighQuality = jpegSection.GetValue("HighQuality", config.Jpeg.HighQuality),
                    StandardQuality = jpegSection.GetValue("StandardQuality", config.Jpeg.StandardQuality),
                    MinQuality = jpegSection.GetValue("MinQuality", config.Jpeg.MinQuality)
                } : config.Jpeg
            });
        }

        // Load quality configuration
        var qualitySection = section.GetSection("Quality");
        if (qualitySection.Exists())
        {
            builder = builder.WithQuality(config => config with
            {
                DefaultThreshold = qualitySection.GetValue("DefaultThreshold", config.DefaultThreshold),
                HighThreshold = qualitySection.GetValue("HighThreshold", config.HighThreshold),
                VeryHighThreshold = qualitySection.GetValue("VeryHighThreshold", config.VeryHighThreshold),
                ArchivalThreshold = qualitySection.GetValue("ArchivalThreshold", config.ArchivalThreshold),
                MinThreshold = qualitySection.GetValue("MinThreshold", config.MinThreshold),
                EnforceComplianceByDefault = qualitySection.GetValue("EnforceComplianceByDefault", config.EnforceComplianceByDefault)
            });
        }

        // Load performance configuration
        var performanceSection = section.GetSection("Performance");
        if (performanceSection.Exists())
        {
            builder = builder.WithPerformance(config => config with
            {
                DefaultTimeout = TimeSpan.FromSeconds(performanceSection.GetValue("DefaultTimeoutSeconds", (int)config.DefaultTimeout.TotalSeconds)),
                FastTimeout = TimeSpan.FromSeconds(performanceSection.GetValue("FastTimeoutSeconds", (int)config.FastTimeout.TotalSeconds)),
                ExtendedTimeout = TimeSpan.FromSeconds(performanceSection.GetValue("ExtendedTimeoutSeconds", (int)config.ExtendedTimeout.TotalSeconds)),
                DefaultRetries = performanceSection.GetValue("DefaultRetries", config.DefaultRetries),
                FastRetries = performanceSection.GetValue("FastRetries", config.FastRetries),
                ExtendedRetries = performanceSection.GetValue("ExtendedRetries", config.ExtendedRetries),
                MaxRetries = performanceSection.GetValue("MaxRetries", config.MaxRetries)
            });
        }

        // Load standard presets configuration
        var standardsSection = section.GetSection("Standards");
        if (standardsSection.Exists())
        {
            builder = builder.WithStandards(config => config with
            {
                TwicMax = new TargetSizePreset(
                    standardsSection.GetValue("TwicMaxBytes", config.TwicMax.TargetBytes),
                    config.TwicMax.Description),
                PivMin = new TargetSizePreset(
                    standardsSection.GetValue("PivMinBytes", config.PivMin.TargetBytes),
                    config.PivMin.Description),
                PivBalanced = new TargetSizePreset(
                    standardsSection.GetValue("PivBalancedBytes", config.PivBalanced.TargetBytes),
                    config.PivBalanced.Description),
                PivHigh = new TargetSizePreset(
                    standardsSection.GetValue("PivHighBytes", config.PivHigh.TargetBytes),
                    config.PivHigh.Description),
                PivVeryHigh = new TargetSizePreset(
                    standardsSection.GetValue("PivVeryHighBytes", config.PivVeryHigh.TargetBytes),
                    config.PivVeryHigh.Description),
                Minimal = new TargetSizePreset(
                    standardsSection.GetValue("MinimalBytes", config.Minimal.TargetBytes),
                    config.Minimal.Description)
            });
        }

        var result = builder.Build();
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Invalid configuration loaded from {sectionName}: {result.Error}");
        }

        return result.Value;
    }

    /// <summary>
    /// Creates ProcessingOptions from BiometricConfiguration and preset name
    /// </summary>
    public static Maybe<Core.Domain.Transformations.ProcessingOptions> GetProcessingOptions(
        this BiometricConfiguration config,
        string presetName)
    {
        return presetName.ToLowerInvariant() switch
        {
            "twic-max" or "twicmax" => Maybe<Core.Domain.Transformations.ProcessingOptions>.From(
                CreateProcessingOptions(config, config.Standards.TwicMax.TargetBytes)),
            "piv-min" or "pivmin" => Maybe<Core.Domain.Transformations.ProcessingOptions>.From(
                CreateProcessingOptions(config, config.Standards.PivMin.TargetBytes)),
            "piv-balanced" or "pivbalanced" => Maybe<Core.Domain.Transformations.ProcessingOptions>.From(
                CreateProcessingOptions(config, config.Standards.PivBalanced.TargetBytes)),
            "piv-high" or "pivhigh" => Maybe<Core.Domain.Transformations.ProcessingOptions>.From(
                CreateProcessingOptions(config, config.Standards.PivHigh.TargetBytes)),
            "piv-very-high" or "pivveryhigh" => Maybe<Core.Domain.Transformations.ProcessingOptions>.From(
                CreateProcessingOptions(config, config.Standards.PivVeryHigh.TargetBytes)),
            "minimal" => Maybe<Core.Domain.Transformations.ProcessingOptions>.From(
                CreateProcessingOptions(config, config.Standards.Minimal.TargetBytes)),
            "archival" => Maybe<Core.Domain.Transformations.ProcessingOptions>.From(
                CreateArchivalProcessingOptions(config)),
            "fast" => Maybe<Core.Domain.Transformations.ProcessingOptions>.From(
                CreateFastProcessingOptions(config)),
            _ => Maybe<Core.Domain.Transformations.ProcessingOptions>.None
        };
    }

    /// <summary>
    /// Creates default ProcessingOptions from BiometricConfiguration
    /// </summary>
    public static Core.Domain.Transformations.ProcessingOptions GetDefaultProcessingOptions(
        this BiometricConfiguration config)
    {
        return CreateProcessingOptions(config, config.Standards.PivBalanced.TargetBytes);
    }

    private static Core.Domain.Transformations.ProcessingOptions CreateProcessingOptions(
        BiometricConfiguration config, 
        int targetSize)
    {
        return new Core.Domain.Transformations.ProcessingOptions
        {
            MinFaceConfidence = config.FaceDetection.MinConfidence,
            RequireSingleFace = config.FaceDetection.RequireSingleFace,
            MaxRetries = config.Performance.DefaultRetries,
            ProcessingTimeout = config.Performance.DefaultTimeout,
            PreserveMetadata = config.ImageProcessing.PreserveMetadata,
            RoiStartLevel = config.Encoding.Jpeg2000.DefaultRoiStartLevel,
            EnableRoi = config.Encoding.Jpeg2000.EnableRoiByDefault,
            AlignRoi = config.Encoding.Jpeg2000.AlignRoiByDefault,
            MaxRotationDegrees = config.FaceDetection.MaxRotationDegrees,
            Strategy = Core.Domain.Transformations.EncodingStrategy.TargetSize(targetSize)
        };
    }

    private static Core.Domain.Transformations.ProcessingOptions CreateArchivalProcessingOptions(
        BiometricConfiguration config)
    {
        return new Core.Domain.Transformations.ProcessingOptions
        {
            MinFaceConfidence = config.FaceDetection.ArchivalMinConfidence,
            RequireSingleFace = config.FaceDetection.RequireSingleFace,
            MaxRetries = config.Performance.ExtendedRetries,
            ProcessingTimeout = config.Performance.ExtendedTimeout,
            PreserveMetadata = true,
            RoiStartLevel = config.Encoding.Jpeg2000.DefaultRoiStartLevel,
            EnableRoi = config.Encoding.Jpeg2000.EnableRoiByDefault,
            AlignRoi = config.Encoding.Jpeg2000.AlignRoiByDefault,
            MaxRotationDegrees = config.FaceDetection.MaxRotationDegrees,
            Strategy = Core.Domain.Transformations.EncodingStrategy.FixedRate(config.Encoding.Jpeg2000.ArchivalRate)
        };
    }

    private static Core.Domain.Transformations.ProcessingOptions CreateFastProcessingOptions(
        BiometricConfiguration config)
    {
        return new Core.Domain.Transformations.ProcessingOptions
        {
            MinFaceConfidence = config.FaceDetection.RelaxedMinConfidence,
            RequireSingleFace = config.FaceDetection.RequireSingleFace,
            MaxRetries = config.Performance.FastRetries,
            ProcessingTimeout = config.Performance.FastTimeout,
            PreserveMetadata = config.ImageProcessing.PreserveMetadata,
            RoiStartLevel = config.Encoding.Jpeg2000.DefaultRoiStartLevel,
            EnableRoi = config.Encoding.Jpeg2000.EnableRoiByDefault,
            AlignRoi = config.Encoding.Jpeg2000.AlignRoiByDefault,
            MaxRotationDegrees = config.FaceDetection.MaxRotationDegrees,
            Strategy = Core.Domain.Transformations.EncodingStrategy.TargetSize(config.Standards.PivBalanced.TargetBytes)
        };
    }

    private static BiometricConfiguration BuildFromOptions(BiometricConfigurationOptions options)
    {
        var builder = BiometricConfigurationBuilder.Create();

        if (options.FaceDetection != null)
        {
            builder = builder.WithFaceDetection(config => options.FaceDetection.ToConfiguration(config));
        }

        if (options.Encoding != null)
        {
            builder = builder.WithEncoding(config => options.Encoding.ToConfiguration(config));
        }

        if (options.Quality != null)
        {
            builder = builder.WithQuality(config => options.Quality.ToConfiguration(config));
        }

        if (options.Performance != null)
        {
            builder = builder.WithPerformance(config => options.Performance.ToConfiguration(config));
        }

        if (options.Standards != null)
        {
            builder = builder.WithStandards(config => options.Standards.ToConfiguration(config));
        }

        var result = builder.Build();
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Invalid options configuration: {result.Error}");
        }

        return result.Value;
    }
}

/// <summary>
/// Options class for binding from IConfiguration
/// </summary>
[PublicAPI]
public sealed class BiometricConfigurationOptions
{
    public FaceDetectionOptions? FaceDetection { get; set; }
    public EncodingOptions? Encoding { get; set; }
    public QualityOptions? Quality { get; set; }
    public PerformanceOptions? Performance { get; set; }
    public StandardsOptions? Standards { get; set; }
}

[PublicAPI]
public sealed class FaceDetectionOptions
{
    public float? MinConfidence { get; set; }
    public float? StrictModeMinConfidence { get; set; }
    public float? ArchivalMinConfidence { get; set; }
    public float? RelaxedMinConfidence { get; set; }
    public bool? RequireSingleFace { get; set; }
    public float? MaxRotationDegrees { get; set; }

    internal FaceDetectionConfiguration ToConfiguration(FaceDetectionConfiguration defaults) => defaults with
    {
        MinConfidence = MinConfidence ?? defaults.MinConfidence,
        StrictModeMinConfidence = StrictModeMinConfidence ?? defaults.StrictModeMinConfidence,
        ArchivalMinConfidence = ArchivalMinConfidence ?? defaults.ArchivalMinConfidence,
        RelaxedMinConfidence = RelaxedMinConfidence ?? defaults.RelaxedMinConfidence,
        RequireSingleFace = RequireSingleFace ?? defaults.RequireSingleFace,
        MaxRotationDegrees = MaxRotationDegrees ?? defaults.MaxRotationDegrees
    };
}

[PublicAPI]
public sealed class EncodingOptions
{
    public string? DefaultFormat { get; set; }
    public Jpeg2000Options? Jpeg2000 { get; set; }
    public JpegOptions? Jpeg { get; set; }

    internal EncodingConfiguration ToConfiguration(EncodingConfiguration defaults) => defaults with
    {
        DefaultFormat = DefaultFormat ?? defaults.DefaultFormat,
        Jpeg2000 = Jpeg2000?.ToConfiguration(defaults.Jpeg2000) ?? defaults.Jpeg2000,
        Jpeg = Jpeg?.ToConfiguration(defaults.Jpeg) ?? defaults.Jpeg
    };
}

[PublicAPI]
public sealed class Jpeg2000Options
{
    public float? DefaultRate { get; set; }
    public float? HighQualityRate { get; set; }
    public float? ArchivalRate { get; set; }
    public int? DefaultRoiStartLevel { get; set; }
    public int? ConservativeRoiStartLevel { get; set; }
    public int? AggressiveRoiStartLevel { get; set; }
    public bool? EnableRoiByDefault { get; set; }
    public bool? AlignRoiByDefault { get; set; }

    internal Jpeg2000Configuration ToConfiguration(Jpeg2000Configuration defaults) => defaults with
    {
        DefaultRate = DefaultRate ?? defaults.DefaultRate,
        HighQualityRate = HighQualityRate ?? defaults.HighQualityRate,
        ArchivalRate = ArchivalRate ?? defaults.ArchivalRate,
        DefaultRoiStartLevel = DefaultRoiStartLevel ?? defaults.DefaultRoiStartLevel,
        ConservativeRoiStartLevel = ConservativeRoiStartLevel ?? defaults.ConservativeRoiStartLevel,
        AggressiveRoiStartLevel = AggressiveRoiStartLevel ?? defaults.AggressiveRoiStartLevel,
        EnableRoiByDefault = EnableRoiByDefault ?? defaults.EnableRoiByDefault,
        AlignRoiByDefault = AlignRoiByDefault ?? defaults.AlignRoiByDefault
    };
}

[PublicAPI]
public sealed class JpegOptions
{
    public int? DefaultQuality { get; set; }
    public int? HighQuality { get; set; }
    public int? StandardQuality { get; set; }
    public int? MinQuality { get; set; }

    internal JpegConfiguration ToConfiguration(JpegConfiguration defaults) => defaults with
    {
        DefaultQuality = DefaultQuality ?? defaults.DefaultQuality,
        HighQuality = HighQuality ?? defaults.HighQuality,
        StandardQuality = StandardQuality ?? defaults.StandardQuality,
        MinQuality = MinQuality ?? defaults.MinQuality
    };
}

[PublicAPI]
public sealed class QualityOptions
{
    public float? DefaultThreshold { get; set; }
    public float? HighThreshold { get; set; }
    public float? VeryHighThreshold { get; set; }
    public float? ArchivalThreshold { get; set; }
    public float? MinThreshold { get; set; }
    public bool? EnforceComplianceByDefault { get; set; }

    internal QualityConfiguration ToConfiguration(QualityConfiguration defaults) => defaults with
    {
        DefaultThreshold = DefaultThreshold ?? defaults.DefaultThreshold,
        HighThreshold = HighThreshold ?? defaults.HighThreshold,
        VeryHighThreshold = VeryHighThreshold ?? defaults.VeryHighThreshold,
        ArchivalThreshold = ArchivalThreshold ?? defaults.ArchivalThreshold,
        MinThreshold = MinThreshold ?? defaults.MinThreshold,
        EnforceComplianceByDefault = EnforceComplianceByDefault ?? defaults.EnforceComplianceByDefault
    };
}

[PublicAPI]
public sealed class PerformanceOptions
{
    public int? DefaultTimeoutSeconds { get; set; }
    public int? FastTimeoutSeconds { get; set; }
    public int? ExtendedTimeoutSeconds { get; set; }
    public int? DefaultRetries { get; set; }
    public int? FastRetries { get; set; }
    public int? ExtendedRetries { get; set; }
    public int? MaxRetries { get; set; }

    internal PerformanceConfiguration ToConfiguration(PerformanceConfiguration defaults) => defaults with
    {
        DefaultTimeout = DefaultTimeoutSeconds.HasValue ? TimeSpan.FromSeconds(DefaultTimeoutSeconds.Value) : defaults.DefaultTimeout,
        FastTimeout = FastTimeoutSeconds.HasValue ? TimeSpan.FromSeconds(FastTimeoutSeconds.Value) : defaults.FastTimeout,
        ExtendedTimeout = ExtendedTimeoutSeconds.HasValue ? TimeSpan.FromSeconds(ExtendedTimeoutSeconds.Value) : defaults.ExtendedTimeout,
        DefaultRetries = DefaultRetries ?? defaults.DefaultRetries,
        FastRetries = FastRetries ?? defaults.FastRetries,
        ExtendedRetries = ExtendedRetries ?? defaults.ExtendedRetries,
        MaxRetries = MaxRetries ?? defaults.MaxRetries
    };
}

[PublicAPI]
public sealed class StandardsOptions
{
    public int? TwicMaxBytes { get; set; }
    public int? PivMinBytes { get; set; }
    public int? PivBalancedBytes { get; set; }
    public int? PivHighBytes { get; set; }
    public int? PivVeryHighBytes { get; set; }
    public int? MinimalBytes { get; set; }

    internal StandardPresetsConfiguration ToConfiguration(StandardPresetsConfiguration defaults) => defaults with
    {
        TwicMax = new TargetSizePreset(TwicMaxBytes ?? defaults.TwicMax.TargetBytes, defaults.TwicMax.Description),
        PivMin = new TargetSizePreset(PivMinBytes ?? defaults.PivMin.TargetBytes, defaults.PivMin.Description),
        PivBalanced = new TargetSizePreset(PivBalancedBytes ?? defaults.PivBalanced.TargetBytes, defaults.PivBalanced.Description),
        PivHigh = new TargetSizePreset(PivHighBytes ?? defaults.PivHigh.TargetBytes, defaults.PivHigh.Description),
        PivVeryHigh = new TargetSizePreset(PivVeryHighBytes ?? defaults.PivVeryHigh.TargetBytes, defaults.PivVeryHigh.Description),
        Minimal = new TargetSizePreset(MinimalBytes ?? defaults.Minimal.TargetBytes, defaults.Minimal.Description)
    };
}