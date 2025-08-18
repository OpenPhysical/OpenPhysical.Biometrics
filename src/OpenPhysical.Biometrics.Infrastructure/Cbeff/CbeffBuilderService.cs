namespace OpenPhysical.Biometrics.Infrastructure.Cbeff;

using CSharpFunctionalExtensions;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Domains.Biometrics;
using OpenPhysical.Domains.Cbeff;
using System;
using System.Threading.Tasks;
using Org.BouncyCastle.X509;

/// <summary>
/// Service for building CBEFF containers from biometric data.
/// </summary>
public sealed class CbeffBuilderService : ICbeffBuilder
{
    private readonly ICbeffEncoder _encoder;
    private readonly ICbeffSigner _signer;
    
    public CbeffBuilderService(ICbeffEncoder encoder, ICbeffSigner signer)
    {
        _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
    }
    
    /// <summary>
    /// Creates a CBEFF container from formatted biometric data.
    /// </summary>
    public async Task<Result<CbeffPayload>> BuildAsync(
        byte[] biometricData,
        BiometricModality modality,
        BiometricFormat format,
        CbeffMetadata metadata)
    {
        if (biometricData == null || biometricData.Length == 0)
            return Result.Failure<CbeffPayload>("Biometric data cannot be null or empty");
            
        try
        {
            // Use the encoder to create the CBEFF payload
            return await _encoder.EncodeAsync(biometricData, modality, format, metadata);
        }
        catch (Exception ex)
        {
            return Result.Failure<CbeffPayload>($"Error building CBEFF container: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Creates a signed CBEFF container from formatted biometric data.
    /// </summary>
    public async Task<Result<CbeffPayload>> BuildSignedAsync(
        byte[] biometricData,
        BiometricModality modality,
        BiometricFormat format,
        CbeffMetadata metadata,
        X509Certificate signingCertificate,
        Maybe<Guid> includeCardUuid = default)
    {
        if (signingCertificate == null)
            return Result.Failure<CbeffPayload>("Signing certificate cannot be null");
            
        // First build the unsigned CBEFF
        var buildResult = await BuildAsync(biometricData, modality, format, metadata);
        if (buildResult.IsFailure)
            return buildResult;
            
        // Then sign it
        return await _signer.SignAsync(buildResult.Value, signingCertificate, includeCardUuid);
    }
}