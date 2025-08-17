# OpenPhysical.Biometrics Architecture

## Overview

OpenPhysical.Biometrics implements a composable, plugin-based architecture that allows consumers to mix and match only the components they need. This design maximizes reusability while maintaining clean separation of concerns.

## Core Design Principles

### 1. Composability First
- Each project provides independent value
- Consumers can use just what they need
- No forced dependencies on unused features

### 2. Clean Dependencies
```
OpenPhysical.Domains (Foundation)
    ↑
OpenPhysical.Biometrics (Core Library)
    ↑
OpenPhysical.Piv (Standard Implementation)
    ↑
OpenPhysical.Biometrics.Tool (CLI Application)
```

### 3. Standards as Plugins
- Core library is standards-agnostic
- Each standard is a separate package
- New standards don't require core changes

## Project Breakdown

### OpenPhysical.Domains
**Purpose**: Shared domain models for all OpenPhysical projects

**Key Types**:
- `BiometricModality` - Face, Fingerprint, Iris, etc.
- `BiometricFormat` - INCITS, ISO/IEC formats with OIDs
- `ImageDimensions` - Standard sizes (PIV, ICAO, etc.)
- `CbeffHeader` - Common CBEFF structure
- `DocumentStandardId` - Standard identifiers
- `ComplianceRequirement` - Threshold specifications

**Dependencies**: None (foundation layer)

### OpenPhysical.Biometrics
**Purpose**: Core biometric processing library

**Capabilities**:
- Face detection (RetinaFace ONNX)
- Landmark extraction (68-point PFLD)
- Quality assessment algorithms
- JPEG 2000 ROI encoding
- Image transformation pipeline

**Key Interfaces**:
```csharp
public interface IDocumentStandard
{
    DocumentStandardId Id { get; }
    ImageDimensions RequiredDimensions { get; }
    IImageProcessor CreateProcessor();
    Task<Result<ComplianceValidation>> ValidateAsync(byte[] imageData);
}

public interface ICbeffFormat
{
    BiometricFormat Format { get; }
    Result<CbeffPayload> Decode(byte[] cbeffData);
    Result<byte[]> Encode(BiometricData data, Maybe<CbeffSignature> signature);
}
```

**Dependencies**: OpenPhysical.Domains only

### OpenPhysical.Piv (Separate Project)
**Purpose**: PIV/TWIC/CAC standard implementations

**Provides**:
- `PivStandard : IDocumentStandard`
- `TwicStandard : IDocumentStandard`
- `CacStandard : IDocumentStandard`
- `PivPatronFormat : ICbeffFormat`
- PIV-specific processors and validators

**Registration**:
```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPivStandards(this IServiceCollection services)
    {
        services.AddSingleton<IDocumentStandard, PivStandard>();
        services.AddSingleton<IDocumentStandard, TwicStandard>();
        services.AddSingleton<IDocumentStandard, CacStandard>();
        services.AddSingleton<ICbeffFormat, PivPatronFormat>();
        return services;
    }
}
```

**Dependencies**: OpenPhysical.Domains, OpenPhysical.Biometrics

### OpenPhysical.Biometrics.Tool
**Purpose**: Command-line interface combining all components

**Features**:
- Standards-based commands (piv, twic, icao)
- Analysis commands (validate, analyze, quality)
- Batch processing
- Interactive modes
- Human-in-the-loop guidance

**Dependencies**: All of the above

## Usage Scenarios

### Scenario 1: Custom Biometric Application
A company building their own biometric system with custom requirements.

```xml
<PackageReference Include="OpenPhysical.Biometrics" Version="3.0.0" />
```

```csharp
services.AddBiometrics();

// Use face detection directly
var detector = services.GetService<IFaceDetector>();
var faces = await detector.DetectFacesAsync(imageData);

// Implement custom standard
public class EmployeeBadgeStandard : IDocumentStandard
{
    public DocumentStandardId Id => new("EMPLOYEE_BADGE");
    public ImageDimensions RequiredDimensions => new(300, 400);
    // ... custom implementation
}
```

### Scenario 2: Government ID Card System
Building a system for PIV/TWIC card issuance.

```xml
<PackageReference Include="OpenPhysical.Biometrics" Version="3.0.0" />
<PackageReference Include="OpenPhysical.Piv" Version="1.0.0" />
```

```csharp
services.AddBiometrics();
services.AddPivStandards();

var registry = services.GetService<IStandardRegistry>();
var pivStandard = registry.GetStandard(DocumentStandardId.Piv);
var result = await pivStandard.ValidateAsync(imageData);
```

### Scenario 3: Passport Photo Service
Online service for passport photo preparation.

```xml
<PackageReference Include="OpenPhysical.Biometrics" Version="3.0.0" />
<!-- Future: <PackageReference Include="OpenPhysical.Icao" Version="1.0.0" /> -->
```

```csharp
services.AddBiometrics();
// services.AddIcaoStandards(); // When available

// For now, implement custom
public class PassportPhotoStandard : IDocumentStandard
{
    public DocumentStandardId Id => new("PASSPORT");
    public ImageDimensions RequiredDimensions => new(413, 531);
    // ... implement ICAO 9303 requirements
}
```

### Scenario 4: Full CLI Tool
Using the complete command-line tool.

```bash
# Install the tool
dotnet tool install -g OpenPhysical.Biometrics.Tool

# Use with any registered standard
biometrics piv photo.jpg
biometrics validate photo.jpg --standard passport
biometrics batch ./photos --standard employee-badge
```

## Extension Points

### Adding a New Standard

1. Create a new project (e.g., OpenPhysical.Icao)
2. Reference OpenPhysical.Biometrics
3. Implement IDocumentStandard
4. Create registration extension
5. Publish as NuGet package

```csharp
public class IcaoStandard : IDocumentStandard
{
    public DocumentStandardId Id => DocumentStandardId.Icao;
    public string Name => "ICAO 9303 Passport";
    public ImageDimensions RequiredDimensions => ImageDimensions.Standards.IcaoType10;
    
    public IImageProcessor CreateProcessor()
    {
        return new IcaoImageProcessor();
    }
    
    public async Task<Result<ComplianceValidation>> ValidateAsync(byte[] imageData)
    {
        // Implement ICAO-specific validation
    }
}
```

### Adding a New Biometric Modality

1. Add modality to OpenPhysical.Domains
2. Create interfaces in Biometrics.Core
3. Implement in Biometrics.Infrastructure
4. Standards can then use the new modality

```csharp
// In Domains
public static BiometricModality Retina { get; } = new("RETINA");

// In Biometrics.Core
public interface IRetinaScanner
{
    Task<Result<RetinaScan>> ScanAsync(byte[] imageData);
}

// Standards can now support retina
public class RetinaStandard : IDocumentStandard
{
    public IReadOnlyList<BiometricModality> SupportedModalities => 
        new[] { BiometricModality.Retina };
}
```

## Benefits

### For Library Authors
- Clear boundaries between projects
- No circular dependencies
- Easy to test in isolation
- Can evolve independently

### For Consumers
- Pay for what you use (size/complexity)
- Easy to understand dependencies
- Can extend without forking
- Standards can be added without waiting for core updates

### For the Ecosystem
- Standards can be community-contributed
- Multiple standards can coexist
- Experimental standards don't affect core
- Clear upgrade paths

## Migration from Monolithic Architecture

### Before (v2.x)
```csharp
// Everything bundled together
using FaceOFFx.Core.Domain;
var processor = new PivProcessor(); // PIV hardcoded
```

### After (v3.x)
```csharp
// Choose your components
using OpenPhysical.Biometrics.Core.Standards;
using OpenPhysical.Piv.Standards;

services.AddBiometrics();
services.AddPivStandards(); // Explicit opt-in

var standard = registry.GetStandard(DocumentStandardId.Piv);
var processor = standard.CreateProcessor();
```

## Future Roadmap

### Near Term
1. Extract PIV code to OpenPhysical.Piv
2. Migrate CBEFF from OpenPhysical.Net
3. Create example projects
4. Publish NuGet packages

### Medium Term
1. OpenPhysical.Icao package
2. OpenPhysical.RealId package
3. Community standard templates
4. Standard validation tools

### Long Term
1. Fingerprint modality support
2. Iris modality support
3. Multi-modal fusion
4. Cloud-native processing options