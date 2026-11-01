#!/bin/bash

# Test script for CBEFF inspection command
# This demonstrates how to inspect CBEFF data files from GSA ICAM test cards

echo "OpenPhysical.Biometrics CBEFF Inspector Test"
echo "==========================================="
echo ""

# Path to GSA ICAM test data
GSA_PATH="/Users/mistial/Projects/gsa-icam-card-builder-v1.8.76/cards/ICAM_Card_Objects/01_Golden_PIV"

# Check if the facial image object exists
if [ -f "$GSA_PATH/10 - Face Object" ]; then
    echo "Found GSA ICAM Golden PIV Face Object"
    echo "Inspecting CBEFF structure..."
    echo ""
    
    # Run the CBEFF inspection command
    dotnet run --project src/OpenPhysical.Biometrics.Tool -- cbeff "$GSA_PATH/10 - Face Object"
    
    echo ""
    echo "Inspecting with hex dump..."
    echo ""
    
    # Run with hex option
    dotnet run --project src/OpenPhysical.Biometrics.Tool -- cbeff "$GSA_PATH/10 - Face Object" --hex
    
    # Extract the biometric data
    echo ""
    echo "Extracting biometric data to facial_image.jp2..."
    dotnet run --project src/OpenPhysical.Biometrics.Tool -- cbeff "$GSA_PATH/10 - Face Object" --extract facial_image.jp2
    
    # Check if extraction was successful
    if [ -f "facial_image.jp2" ]; then
        echo "Successfully extracted facial image data"
        ls -la facial_image.jp2
    fi
else
    echo "Error: Supply GSA ICAM test data at the expected location"
    echo "Please ensure gsa-icam-card-builder is at: $GSA_PATH"
    exit 1
fi

echo ""
echo "Test complete!"
