using System.Collections.Immutable;
using NUnit.Framework;
using FluentAssertions;
using OpenPhysical.Biometrics.Infrastructure.Cbeff;

namespace OpenPhysical.Biometrics.Infrastructure.Tests.Cbeff;

[TestFixture]
public class CbeffDebugTest
{
    [Test]
    public void Debug_DateTimeCodec_WithExactBytes()
    {
        // Test the exact bytes from SDS FMR PIV file
        var dateBytes = new byte[] { 0x14, 0x06, 0x09, 0x1a, 0x0c, 0x23, 0x1d, 0x5a };
        
        Console.WriteLine($"Date bytes: {BitConverter.ToString(dateBytes)}");
        Console.WriteLine($"Last byte: {dateBytes[7]} (should be 90)");
        
        var result = DateTimeCodec.Decode(dateBytes);
        
        if (result.IsFailure)
        {
            Console.WriteLine($"Decode failed: {result.Error}");
            result.IsSuccess.Should().BeTrue($"DateTimeCodec should decode valid bytes but failed: {result.Error}");
        }
        else
        {
            Console.WriteLine($"Decoded date: {result.Value}");
            result.Value.Year.Should().Be(2006);
            result.Value.Month.Should().Be(9);
            result.Value.Day.Should().Be(26);
            result.Value.Hour.Should().Be(12);
            result.Value.Minute.Should().Be(35);
            result.Value.Second.Should().Be(29);
        }
    }

    [Test]
    public void Debug_CbeffHeaderSerializer_StepByStep()
    {
        // Use the exact 88 bytes from the SDS FMR PIV file
        var headerBytes = new byte[] {
            0x03, 0x0d, 0x00, 0x00, 0x04, 0x40, 0x00, 0x00, 0x00, 0x1b, 0x02, 0x01, 0x14, 0x06, 0x09, 0x1a,
            0x0c, 0x23, 0x1d, 0x5a, 0x14, 0x06, 0x09, 0x1a, 0x0c, 0x23, 0x1d, 0x5a, 0x14, 0x10, 0x09, 0x1a,
            0x0c, 0x23, 0x1d, 0x5a, 0x00, 0x00, 0x08, 0x80, 0x64, 0x4e, 0x49, 0x53, 0x54, 0x2d, 0x49, 0x41,
            0x44, 0x00, 0xff, 0x8f, 0xe5, 0xc5, 0x68, 0x00, 0x10, 0x06, 0x90, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };
        
        Console.WriteLine($"Header length: {headerBytes.Length}");
        
        // Debug step by step
        Console.WriteLine($"Patron version: {headerBytes[0]}");
        Console.WriteLine($"SBH security: {headerBytes[1]}");
        Console.WriteLine($"BDB length: {BitConverter.ToUInt32(headerBytes.Skip(2).Take(4).Reverse().ToArray())}");
        Console.WriteLine($"SB length: {BitConverter.ToUInt16(headerBytes.Skip(6).Take(2).Reverse().ToArray())}");
        Console.WriteLine($"Format owner: {BitConverter.ToUInt16(headerBytes.Skip(8).Take(2).Reverse().ToArray())}");
        Console.WriteLine($"Format type: {BitConverter.ToUInt16(headerBytes.Skip(10).Take(2).Reverse().ToArray())}");
        
        // Test each date individually
        var creationBytes = headerBytes.Skip(12).Take(8).ToArray();
        var validityStartBytes = headerBytes.Skip(20).Take(8).ToArray();
        var validityEndBytes = headerBytes.Skip(28).Take(8).ToArray();
        
        Console.WriteLine($"Creation date bytes: {BitConverter.ToString(creationBytes)}");
        Console.WriteLine($"Validity start bytes: {BitConverter.ToString(validityStartBytes)}");
        Console.WriteLine($"Validity end bytes: {BitConverter.ToString(validityEndBytes)}");
        
        var creationResult = DateTimeCodec.Decode(creationBytes);
        var startResult = DateTimeCodec.Decode(validityStartBytes);
        var endResult = DateTimeCodec.Decode(validityEndBytes);
        
        Console.WriteLine($"Creation decode: {(creationResult.IsSuccess ? creationResult.Value.ToString() : creationResult.Error)}");
        Console.WriteLine($"Start decode: {(startResult.IsSuccess ? startResult.Value.ToString() : startResult.Error)}");
        Console.WriteLine($"End decode: {(endResult.IsSuccess ? endResult.Value.ToString() : endResult.Error)}");
        
        // Now try the full header parsing
        Console.WriteLine("Attempting full header parse...");
        var result = CbeffHeaderSerializer.FromBytes(headerBytes);
        
        if (result.IsFailure)
        {
            Console.WriteLine($"Full header parse failed: {result.Error}");
            result.IsSuccess.Should().BeTrue($"Header parsing should work but failed: {result.Error}");
        }
        else
        {
            Console.WriteLine("Full header parse succeeded!");
            Console.WriteLine($"Parsed creation date: {result.Value.CreationDate}");
        }
    }
}