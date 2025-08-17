# OpenPhysical.Biometrics Refactoring Plan

## Overview

Refactor FaceOFFx into a composable biometric architecture where:
- **OpenPhysical.Biometrics**: Pure library for biometric processing (no CLI, no specific standards)
- **OpenPhysical.Piv**: PIV/TWIC/CAC standard implementations (plugin)
- **OpenPhysical.Biometrics.Tool**: CLI that combines library + standards
- **OpenPhysical.Domains**: Shared domain models (already exists)

This allows consumers to:
- Use just the biometrics library for custom standards
- Combine biometrics + PIV for government ID cards
- Combine biometrics + (future) ICAO for passports
- Use the full CLI tool with all standards

## Phase 1: Project Structure (COMPLETED)

### 1.1 Core Projects (Already Renamed)
- ✅ `FaceOFFx.Core` → `OpenPhysical.Biometrics.Core`
- ✅ `FaceOFFx.Infrastructure` → `OpenPhysical.Biometrics.Infrastructure`
- ✅ `FaceOFFx.Models` → `OpenPhysical.Biometrics.Onnx`
- ✅ `FaceOFFx.Cli` → `OpenPhysical.Biometrics.Tool`
- ✅ `FaceOFFx` → `OpenPhysical.Biometrics`

### 1.2 Final Architecture
```
# Dependency Flow (bottom to top)
OpenPhysical.Domains/               # Pure domain models (no dependencies)
    ↑
OpenPhysical.Biometrics/           # Core library (depends only on Domains)
├── Core/
│   ├── Standards/                 # IDocumentStandard interface
│   ├── Cbeff/                     # ICbeffFormat interface
│   └── Abstractions/              # Core interfaces
└── Infrastructure/                # Implementations
    ↑
OpenPhysical.Piv/                  # PIV plugin (depends on Domains + Biometrics)
├── Standards/                     # PivStandard, TwicStandard implementations
├── Cbeff/                         # PIV patron formats
└── Extensions/                    # AddPivStandards()
    ↑
OpenPhysical.Biometrics.Tool/      # CLI (references all)
```

## Phase 2: Domain Integration (IN PROGRESS)

### 2.1 Created in OpenPhysical.Domains
- ✅ `Biometrics/BiometricModality.cs` - Face, Fingerprint, Iris, etc.
- ✅ `Biometrics/BiometricFormat.cs` - Format specifications with OIDs
- ✅ `Biometrics/ImageDimensions.cs` - Standard dimensions (PIV, ICAO, etc.)
- ✅ `Cbeff/CbeffHeader.cs` - Generic CBEFF header structure
- ✅ `Standards/DocumentStandardId.cs` - PIV, TWIC, ICAO identifiers
- ✅ `Standards/ComplianceRequirement.cs` - Threshold definitions

### 2.2 Created Interfaces in Biometrics.Core
- ✅ `Standards/IDocumentStandard.cs` - Interface for all standards
- ✅ `Standards/IStandardRegistry.cs` - Registry for plugin architecture
- ✅ `Cbeff/ICbeffFormat.cs` - Interface for CBEFF implementations

### 2.3 Next Steps
- ⏳ Migrate CBEFF implementation from OpenPhysical.Net
- ⏳ Update existing code to use new domain models
- ⏳ Remove local value objects (Point2D, etc.)

## Phase 3: Create OpenPhysical.Piv Project

### 3.1 Extract PIV-Specific Code
```csharp
// Move from Biometrics.Core to Piv project:
- Domain/Standards/PivConstants.cs → Piv/Standards/PivStandard.cs
- Domain/Standards/PivComplianceValidation.cs → Piv/Validation/
- Domain/Transformations/PivProcessor.cs → Piv/Processing/
```

### 3.2 Implement Standard Interfaces
```csharp
public class PivStandard : IDocumentStandard
{
    public DocumentStandardId Id => DocumentStandardId.Piv;
    public ImageDimensions RequiredDimensions => ImageDimensions.Standards.Piv;
    public IImageProcessor CreateProcessor() => new PivImageProcessor();
}

public class TwicStandard : IDocumentStandard { ... }
public class CacStandard : IDocumentStandard { ... }
```

### 3.3 PIV-Specific CBEFF
```csharp
public class PivPatronFormat : ICbeffFormat
{
    public BiometricFormat Format => BiometricFormat.WellKnown.PivPatronFormatA;
    public Result<CbeffPayload> Decode(byte[] data) { ... }
}
```

## Phase 4: Clean Library Separation

### 4.1 OpenPhysical.Biometrics (Pure Library)
- Remove all CLI-specific code (already in Tool)
- Remove all PIV-specific code (move to Piv)
- Keep only:
  - Core abstractions and interfaces
  - Face detection and landmark extraction
  - Quality assessment algorithms
  - Generic image processing
  - Standards registry infrastructure

### 4.2 Update References
```xml
<!-- OpenPhysical.Biometrics.csproj -->
<ProjectReference Include="OpenPhysical.Domains" />

<!-- OpenPhysical.Piv.csproj -->
<ProjectReference Include="OpenPhysical.Domains" />
<ProjectReference Include="OpenPhysical.Biometrics" />

<!-- OpenPhysical.Biometrics.Tool.csproj -->
<ProjectReference Include="OpenPhysical.Biometrics" />
<ProjectReference Include="OpenPhysical.Piv" />
```

## Phase 5: Universal CLI in Tool Project

### 5.1 Standards-Based Commands
```bash
# Government ID processing
biometrics piv photo.jpg          # Uses OpenPhysical.Piv
biometrics twic photo.jpg         # Uses OpenPhysical.Piv
biometrics icao photo.jpg         # Future: Uses OpenPhysical.Icao

# General purpose
biometrics photo enhance.jpg      # Uses core library only
biometrics validate photo.jpg --standard piv
biometrics analyze photo.jpg
```

### 5.2 Plugin Registration
```csharp
// Program.cs in Tool project
services.AddBiometrics();           // Core services
services.AddPivStandards();         // From OpenPhysical.Piv
// Future: services.AddIcaoStandards();

var registry = services.GetService<IStandardRegistry>();
// All standards now available to CLI
```

## Phase 6: Documentation and Examples

### 6.1 Library Usage Examples
```csharp
// Using just the library (no standards)
services.AddBiometrics();
var detector = services.GetService<IFaceDetector>();
var result = await detector.DetectFacesAsync(imageData);

// Using library + PIV
services.AddBiometrics();
services.AddPivStandards();
var standard = registry.GetStandard(DocumentStandardId.Piv);
var processor = standard.CreateProcessor();

// Custom standard implementation
public class PassportStandard : IDocumentStandard
{
    public DocumentStandardId Id => new("PASSPORT");
    public ImageDimensions RequiredDimensions => new(413, 531);
    // ... implement passport requirements
}
```

### 6.2 Update Documentation
- README.md - Add "Using as a Library" section
- CLAUDE.md - Document new architecture
- Create ARCHITECTURE.md with detailed design
- Add examples/ directory with sample code

## Implementation Order

### Completed ✅
1. **Project renaming** - All projects renamed to OpenPhysical.Biometrics.*
2. **Namespace updates** - All namespaces updated
3. **Domain models created** - Added to OpenPhysical.Domains
4. **Interfaces created** - IDocumentStandard, ICbeffFormat, IStandardRegistry
5. **Tool separated** - CLI already in separate Tool project

### Next Steps ⏳
1. **Create OpenPhysical.Piv project**
   - Extract PIV-specific code from Core
   - Implement IDocumentStandard for PIV/TWIC/CAC
   - Move PIV CBEFF formats

2. **Migrate CBEFF from OpenPhysical.Net**
   - Copy generic CBEFF to Biometrics
   - Copy PIV-specific CBEFF to Piv

3. **Update ProcessCommand**
   - Accept --standard parameter
   - Use IStandardRegistry
   - Support multiple output standards

4. **Create example projects**
   - Library-only usage
   - Library + PIV usage
   - Custom standard implementation

5. **Package and publish**
   - OpenPhysical.Biometrics NuGet
   - OpenPhysical.Piv NuGet
   - OpenPhysical.Biometrics.Tool as dotnet tool

## Benefits of New Architecture

### For Library Users
- **Minimal dependencies**: Just biometrics processing, no CLI overhead
- **Standards agnostic**: Implement your own IDocumentStandard
- **Composable**: Mix and match only what you need

### For PIV Users
- **Complete PIV support**: PIV, TWIC, CAC in one package
- **No unnecessary code**: Skip ICAO/passport if not needed
- **Easy integration**: `services.AddPivStandards()`

### For CLI Users
- **All standards included**: One tool for everything
- **Extensible**: New standards can be added as plugins
- **Familiar interface**: Same commands, more standards

## Migration Guide

### Library-Only Users (New Option!)
```csharp
// Just biometric processing, no standards
<PackageReference Include="OpenPhysical.Biometrics" />

services.AddBiometrics();
var detector = services.GetService<IFaceDetector>();
```

### PIV Application Users
```csharp
// Old: Everything bundled
using FaceOFFx.Core.Domain;
var processor = new PivProcessor();

// New: Composable architecture
<PackageReference Include="OpenPhysical.Biometrics" />
<PackageReference Include="OpenPhysical.Piv" />

services.AddBiometrics();
services.AddPivStandards();
var standard = registry.GetStandard(DocumentStandardId.Piv);
```

### CLI Users
```bash
# Old
faceoffx process photo.jpg

# New (same command, more options)
biometrics process photo.jpg          # Default PIV
biometrics piv photo.jpg              # Explicit PIV
biometrics twic photo.jpg             # TWIC variant
biometrics validate photo.jpg --standard icao
```