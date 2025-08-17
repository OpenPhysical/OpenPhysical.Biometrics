namespace OpenPhysical.Biometrics.Tool.Commands;

using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OpenPhysical.Biometrics.Core.Cbeff;
using OpenPhysical.Biometrics.Core.Cbeff.Formats;
using OpenPhysical.Biometrics.Infrastructure.Cbeff;
using OpenPhysical.Domains.Cbeff;
using OpenPhysical.Domains.Cbeff.Formats.Incits385;

/// <summary>
/// Command to inspect CBEFF data object files and display their structure.
/// </summary>
public sealed class InspectCbeffCommand : AsyncCommand<InspectCbeffCommand.Settings>
{
    private readonly IServiceProvider _serviceProvider;

    public InspectCbeffCommand(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public sealed class Settings : CommandSettings
    {
        [Description("Path to the CBEFF data file to inspect")]
        [CommandArgument(0, "<FILE>")]
        public string FilePath { get; init; } = string.Empty;

        [Description("Show raw hex dump of data sections")]
        [CommandOption("--hex")]
        public bool ShowHex { get; init; }

        [Description("Verify signature if present")]
        [CommandOption("--verify")]
        public bool VerifySignature { get; init; }

        [Description("Path to signer certificate for verification")]
        [CommandOption("--cert")]
        public string? CertificatePath { get; init; }

        [Description("Extract biometric data to file")]
        [CommandOption("--extract")]
        public string? ExtractPath { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            if (!File.Exists(settings.FilePath))
            {
                AnsiConsole.MarkupLine($"[red]File not found:[/] {settings.FilePath}");
                return 1;
            }

            var cbeffData = await File.ReadAllBytesAsync(settings.FilePath);
            AnsiConsole.MarkupLine($"[green]Reading CBEFF file:[/] {settings.FilePath}");
            AnsiConsole.MarkupLine($"[dim]File size: {cbeffData.Length:N0} bytes[/]");
            AnsiConsole.WriteLine();

            // Decode the CBEFF data with format registry support
            var serializer = _serviceProvider.GetRequiredService<ICbeffSerializerExtended>();
            var decodeResult = serializer.DeserializeAutoExtended(cbeffData);

            if (decodeResult.IsFailure)
            {
                AnsiConsole.MarkupLine($"[red]Failed to decode CBEFF data:[/] {decodeResult.Error}");
                return 1;
            }

            var payload = decodeResult.Value;
            DisplayCbeffStructure(payload, settings);

            // Extract biometric data if requested
            if (!string.IsNullOrEmpty(settings.ExtractPath))
            {
                await ExtractBiometricData(payload, settings.ExtractPath);
            }

            // Verify signature if requested
            if (settings.VerifySignature && payload.Signature.HasValue)
            {
                await VerifySignature(payload, settings);
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private void DisplayCbeffStructure(CbeffPayloadExtended payload, Settings settings)
    {
        var tree = new Tree("[yellow]CBEFF Structure[/]");

        // Header section
        var headerNode = tree.AddNode("[green]Header[/] (88 bytes)");
        var header = payload.Header;
        
        headerNode.AddNode($"Patron Header Version: 0x{header.PatronHeaderVersion:X2}");
        headerNode.AddNode($"Security Options: 0x{header.SbhSecurityOptions:X2} ({GetSecurityOptionsDescription(header.SbhSecurityOptions)})");
        headerNode.AddNode($"BDB Length: {header.BdbLength:N0} bytes");
        headerNode.AddNode($"SB Length: {header.SbLength:N0} bytes");
        headerNode.AddNode($"BDB Format Owner: 0x{header.BdbFormatOwner:X4} ({GetFormatOwnerName(header.BdbFormatOwner)})");
        headerNode.AddNode($"BDB Format Type: 0x{header.BdbFormatType:X4} ({GetFormatTypeName(header.BdbFormatOwner, header.BdbFormatType)})");
        
        // Dates
        var datesNode = headerNode.AddNode("[cyan]Dates[/]");
        datesNode.AddNode($"Creation: {header.CreationDate:yyyy-MM-dd HH:mm:ss}");
        datesNode.AddNode($"Valid From: {header.ValidityNotBefore:yyyy-MM-dd HH:mm:ss}");
        datesNode.AddNode($"Valid To: {header.ValidityNotAfter:yyyy-MM-dd HH:mm:ss}");
        
        // Biometric info
        var bioNode = headerNode.AddNode("[cyan]Biometric Info[/]");
        bioNode.AddNode($"Type: 0x{header.BiometricType:X8} ({GetBiometricTypeName(header.BiometricType)})");
        bioNode.AddNode($"Subtype: 0x{header.BiometricSubtype:X2}");
        bioNode.AddNode($"Quality: {header.Quality} ({GetQualityDescription(header.Quality)})");
        
        // Creator and FASC-N
        var metaNode = headerNode.AddNode("[cyan]Metadata[/]");
        metaNode.AddNode($"Creator: {GetCreatorString(header.Creator)}");
        metaNode.AddNode($"FASC-N: {BitConverter.ToString(header.FascN).Replace("-", "")}");

        // Biometric Data Block
        var bdbNode = tree.AddNode($"[green]Biometric Data Block[/] ({payload.BiometricDataBlock.Length:N0} bytes)");
        
        if (settings.ShowHex)
        {
            var hexDump = GetHexDump(payload.BiometricDataBlock.ToArray(), 32);
            bdbNode.AddNode($"[dim]{hexDump}[/]");
        }
        else
        {
            AnalyzeBiometricData(bdbNode, payload);
        }

        // Signature Block
        if (payload.Signature.HasValue)
        {
            var sigNode = tree.AddNode($"[green]Signature Block[/] ({payload.Signature.Value.SignatureSize:N0} bytes)");
            sigNode.AddNode($"Type: {payload.Signature.Value.SignatureType}");
            
            if (settings.ShowHex)
            {
                var hexDump = GetHexDump(payload.Signature.Value.SignatureData.ToArray(), 32);
                sigNode.AddNode($"[dim]{hexDump}[/]");
            }
        }
        else
        {
            tree.AddNode("[dim]No Signature[/]");
        }

        AnsiConsole.Write(tree);
    }

    private void AnalyzeBiometricData(TreeNode parent, CbeffPayloadExtended payload)
    {
        // Check if we have parsed data from the format registry
        if (payload.ParsedBiometricRecord.HasValue)
        {
            var record = payload.ParsedBiometricRecord.Value;
            
            // Display format-specific parsed data
            if (record is OpenPhysical.Biometrics.Infrastructure.Cbeff.Formats.Incits385BiometricRecord incits385)
            {
                DisplayParsedFacialImage(parent, incits385.FacialRecord);
            }
            else
            {
                // Generic display for other record types
                parent.AddNode($"[green]Parsed {record.GetType().Name}[/]");
            }
        }
        else
        {
            // Fall back to basic analysis if no parser available
            var header = payload.Header;
            var data = payload.BiometricDataBlock.ToArray();
            
            if (header.BdbFormatOwner == 0x001B && header.BdbFormatType == 0x0501)
            {
                // INCITS 385 Facial Image
                AnalyzeFacialImage(parent, data);
            }
            else if (header.BdbFormatOwner == 0x001B && header.BdbFormatType == 0x0201)
            {
                // INCITS 378 Fingerprint Template
                AnalyzeFingerprintTemplate(parent, data);
            }
            else
            {
                parent.AddNode("[dim]Unknown biometric format[/]");
            }
        }
    }

    private void AnalyzeFacialImage(TreeNode parent, byte[] data)
    {
        try
        {
            var offset = 0;
            
            // Read format identifier (4 bytes)
            var formatId = Encoding.ASCII.GetString(data, offset, 4);
            offset += 4;
            
            // Read version (4 bytes)
            var version = Encoding.ASCII.GetString(data, offset, 4);
            offset += 4;
            
            // Read record length (4 bytes, big-endian)
            var recordLength = (data[offset] << 24) | (data[offset + 1] << 16) | 
                              (data[offset + 2] << 8) | data[offset + 3];
            offset += 4;
            
            // Read number of facial records (2 bytes, big-endian)
            var numRecords = (data[offset] << 8) | data[offset + 1];
            offset += 2;
            
            parent.AddNode($"Format: {formatId.TrimEnd('\0')} v{version.TrimEnd('\0')}");
            parent.AddNode($"Record Length: {recordLength:N0} bytes");
            parent.AddNode($"Number of Facial Records: {numRecords}");
            
            // For each facial record...
            for (int i = 0; i < numRecords && offset < data.Length - 20; i++)
            {
                var facialNode = parent.AddNode($"[cyan]Facial Record {i + 1}[/]");
                
                // Facial block length (4 bytes)
                var blockLength = (data[offset] << 24) | (data[offset + 1] << 16) | 
                                 (data[offset + 2] << 8) | data[offset + 3];
                offset += 4;
                
                facialNode.AddNode($"Block Length: {blockLength:N0} bytes");
                
                // Skip facial information details for brevity
                offset += 16; // Skip to image info
                
                if (offset + 9 < data.Length)
                {
                    var imageType = data[offset];
                    var dataType = data[offset + 1];
                    var width = (data[offset + 2] << 8) | data[offset + 3];
                    var height = (data[offset + 4] << 8) | data[offset + 5];
                    
                    facialNode.AddNode($"Image Type: {GetImageTypeName(imageType)}");
                    facialNode.AddNode($"Data Type: {GetDataTypeName(dataType)}");
                    facialNode.AddNode($"Dimensions: {width}×{height}");
                }
            }
        }
        catch
        {
            parent.AddNode("[red]Error parsing facial image data[/]");
        }
    }

    private void DisplayParsedFacialImage(TreeNode parent, FacialImageRecord record)
    {
        parent.AddNode($"[green]INCITS 385 Facial Image Record[/]");
        
        // Header information
        var headerNode = parent.AddNode("[cyan]Facial Record Header[/]");
        headerNode.AddNode($"Format: {record.Header.FormatIdentifier} v{record.Header.VersionNumber}");
        headerNode.AddNode($"Record Length: {record.Header.RecordLength:N0} bytes");
        headerNode.AddNode($"Number of Facial Records: {record.Header.NumberOfFaces}");
        
        // Facial information
        var facialNode = parent.AddNode("[cyan]Facial Information[/]");
        facialNode.AddNode($"Block Length: {record.FacialInfo.FaceImageBlockLength:N0} bytes");
        facialNode.AddNode($"Number of Feature Points: {record.FacialInfo.NumberOfFeaturePoints}");
        facialNode.AddNode($"Gender: {GetGenderName(record.FacialInfo.Gender)}");
        facialNode.AddNode($"Eye Color: {GetEyeColorName(record.FacialInfo.EyeColor)}");
        facialNode.AddNode($"Hair Color: {GetHairColorName(record.FacialInfo.HairColor)}");
        facialNode.AddNode($"Feature Mask: 0x{record.FacialInfo.FeatureMask:X6}");
        facialNode.AddNode($"Expression: {GetExpressionName(record.FacialInfo.Expression)}");
        facialNode.AddNode($"Pose Angle: 0x{record.FacialInfo.PoseAngle:X6}");
        facialNode.AddNode($"Pose Angle Uncertainty: 0x{record.FacialInfo.PoseAngleUncertainty:X6}");
        
        // Feature points
        if (record.FeaturePoints.Length > 0)
        {
            var featuresNode = parent.AddNode($"[cyan]Feature Points ({record.FeaturePoints.Length})[/]");
            foreach (var point in record.FeaturePoints.Take(5)) // Show first 5
            {
                featuresNode.AddNode($"Type: {GetFeatureTypeName(point.FeatureType)}, X: {point.HorizontalPosition}, Y: {point.VerticalPosition}");
            }
            if (record.FeaturePoints.Length > 5)
            {
                featuresNode.AddNode($"[dim]... and {record.FeaturePoints.Length - 5} more[/]");
            }
        }
        
        // Image information
        var imageNode = parent.AddNode("[cyan]Image Information[/]");
        imageNode.AddNode($"Type: {GetImageTypeName(record.ImageInfo.FacialImageType)}");
        imageNode.AddNode($"Data Type: {GetDataTypeName(record.ImageInfo.ImageDataType)}");
        imageNode.AddNode($"Dimensions: {record.ImageInfo.Width}×{record.ImageInfo.Height}");
        imageNode.AddNode($"Color Space: {GetColorSpaceName(record.ImageInfo.ImageColorSpace)}");
        imageNode.AddNode($"Source Type: {GetSourceTypeName(record.ImageInfo.SourceType)}");
        imageNode.AddNode($"Quality: {record.ImageInfo.Quality}");
        
        // Image data
        imageNode.AddNode($"Image Data: {record.ImageData.Length:N0} bytes");
    }
    
    private string GetGenderName(byte gender) => gender switch
    {
        0x00 => "Unspecified",
        0x01 => "Male",
        0x02 => "Female",
        0xFF => "Unknown",
        _ => $"Invalid ({gender})"
    };
    
    private string GetEyeColorName(byte color) => color switch
    {
        0x00 => "Unspecified",
        0x01 => "Blue",
        0x02 => "Brown",
        0x03 => "Gray",
        0x04 => "Green",
        0x05 => "Hazel",
        0x06 => "Maroon",
        0x07 => "Pink",
        0x08 => "Multi-colored",
        0xFF => "Unknown",
        _ => $"Invalid ({color})"
    };
    
    private string GetHairColorName(byte color) => color switch
    {
        0x00 => "Unspecified",
        0x01 => "Bald",
        0x02 => "Black",
        0x03 => "Blonde",
        0x04 => "Brown",
        0x05 => "Gray",
        0x06 => "Red",
        0x07 => "Blue",
        0x08 => "Green",
        0xFF => "Unknown",
        _ => $"Invalid ({color})"
    };
    
    private string GetExpressionName(ushort expression) => expression switch
    {
        0x0000 => "Unspecified",
        0x0001 => "Neutral",
        0x0002 => "Smile - closed mouth",
        0x0003 => "Smile - open mouth",
        0x0004 => "Raised eyebrows",
        0x0005 => "Eyes looking away",
        0x0006 => "Squinting",
        0x0007 => "Frowning",
        _ => $"Unknown ({expression})"
    };
    
    private string GetColorSpaceName(byte colorSpace) => colorSpace switch
    {
        0x00 => "Unspecified",
        0x01 => "24-bit RGB",
        0x02 => "YUV422",
        0x03 => "8-bit grayscale",
        0x04 => "Other",
        _ => $"Unknown ({colorSpace})"
    };
    
    private string GetSourceTypeName(byte sourceType) => sourceType switch
    {
        0x00 => "Unspecified",
        0x01 => "Static photograph - unknown source",
        0x02 => "Static photograph - digital camera",
        0x03 => "Static photograph - scanner",
        0x04 => "Video frame - unknown source",
        0x05 => "Video frame - analog camera",
        0x06 => "Video frame - digital camera",
        0x07 => "Unknown",
        _ => $"Invalid ({sourceType})"
    };
    
    private string GetFeatureTypeName(byte type) => type switch
    {
        0x01 => "Right eye center",
        0x02 => "Left eye center",
        0x03 => "Nose tip",
        0x04 => "Nose center",
        0x05 => "Mouth center",
        0x06 => "Right mouth corner",
        0x07 => "Left mouth corner",
        _ => $"Type {type}"
    };

    private void AnalyzeFingerprintTemplate(TreeNode parent, byte[] data)
    {
        try
        {
            var offset = 0;
            
            // Read format identifier (4 bytes)
            var formatId = Encoding.ASCII.GetString(data, offset, 4);
            offset += 4;
            
            // Read version (4 bytes)
            var version = Encoding.ASCII.GetString(data, offset, 4);
            offset += 4;
            
            parent.AddNode($"Format: {formatId.TrimEnd('\0')} v{version.TrimEnd('\0')}");
            
            // Skip to number of finger records
            offset = 24;
            if (offset < data.Length)
            {
                var numFingers = data[offset];
                parent.AddNode($"Number of Finger Records: {numFingers}");
                
                // Check if this is an empty template
                if (data.Length == 38 && numFingers == 2)
                {
                    parent.AddNode("[yellow]Empty template (individual cannot be fingerprinted)[/]");
                }
            }
        }
        catch
        {
            parent.AddNode("[red]Error parsing fingerprint template data[/]");
        }
    }

    private async Task ExtractBiometricData(CbeffPayloadExtended payload, string outputPath)
    {
        try
        {
            await File.WriteAllBytesAsync(outputPath, payload.BiometricDataBlock.ToArray());
            AnsiConsole.MarkupLine($"[green]Extracted biometric data to:[/] {outputPath}");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to extract data:[/] {ex.Message}");
        }
    }

    private async Task VerifySignature(CbeffPayloadExtended payload, Settings settings)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Signature Verification[/]");
        
        // TODO: Implement signature verification using the verifier service
        AnsiConsole.MarkupLine("[dim]Signature verification not yet implemented[/]");
    }

    private string GetHexDump(byte[] data, int maxBytes)
    {
        var bytes = data.Take(maxBytes).ToArray();
        var hex = BitConverter.ToString(bytes).Replace("-", " ");
        if (data.Length > maxBytes)
        {
            hex += " ...";
        }
        return hex;
    }

    private string GetSecurityOptionsDescription(byte options) => options switch
    {
        0x00 => "No security",
        0x08 => "Integrity",
        0x0C => "Integrity and confidentiality",
        0x0D => "Signed plaintext",
        _ => "Unknown"
    };

    private string GetFormatOwnerName(ushort owner) => owner switch
    {
        0x001B => "INCITS TC M1",
        0x0101 => "PIV Patron Format",
        _ => "Unknown"
    };

    private string GetFormatTypeName(ushort owner, ushort type) => (owner, type) switch
    {
        (0x001B, 0x0501) => "Facial Image (INCITS 385)",
        (0x001B, 0x0201) => "Fingerprint Minutiae (INCITS 378)",
        (0x0101, 0x0008) => "PIV Patron Format A",
        (0x0101, 0x0009) => "PIV Patron Format B",
        _ => "Unknown"
    };

    private string GetBiometricTypeName(uint type) => (type & 0xFF) switch
    {
        0x02 => "Face",
        0x08 => "Fingerprint",
        0x10 => "Iris",
        0x20 => "Voice",
        0x40 => "Signature",
        _ => "Unknown"
    };

    private string GetQualityDescription(sbyte quality) => quality switch
    {
        -2 => "Permanently unusable (254 as byte)",
        -1 => "Temporarily unusable (255 as byte)",
        >= 0 and <= 100 => $"{quality}%",
        _ => "Invalid"
    };

    private string GetCreatorString(byte[] creator)
    {
        var str = Encoding.ASCII.GetString(creator).TrimEnd('\0');
        return string.IsNullOrWhiteSpace(str) ? "<empty>" : str;
    }

    private string GetImageTypeName(byte type) => type switch
    {
        0x01 => "Full frontal",
        0x02 => "Token frontal",
        _ => "Unknown"
    };

    private string GetDataTypeName(byte type) => type switch
    {
        0x00 => "JPEG",
        0x01 => "JPEG 2000",
        _ => "Unknown"
    };
}