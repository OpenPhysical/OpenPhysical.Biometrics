#!/bin/bash

# Blur progression test script for FaceOFFx
# Creates incremental blur versions and tests sharpness detection

set -e

# Check if ImageMagick is installed
if ! command -v convert &> /dev/null; then
    echo "Error: Install ImageMagick to run this test."
    echo "Install with: brew install imagemagick"
    exit 1
fi

# Directories
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
FACES_DIR="$SCRIPT_DIR/faces"
BLURRED_DIR="$SCRIPT_DIR/blurred"
RESULTS_DIR="$SCRIPT_DIR/results"

# Create directories
mkdir -p "$BLURRED_DIR"
mkdir -p "$RESULTS_DIR"

# CLI path (relative to script location)
CLI_PATH="$SCRIPT_DIR/../../src/FaceOFFx.Cli/bin/Debug/net8.0/FaceOFFx.Cli"

# Function to create blurred versions
create_blur_progression() {
    local input_file=$1
    local base_name=$(basename "$input_file" .png)
    
    echo "Creating blur progression for $base_name..."
    
    # Create blur levels from 0% to 10% in 1% increments
    for blur_percent in {0..10}; do
        # Convert blur percentage to sigma value
        # Sigma ~= blur_percent * 0.5 for reasonable visual effect
        sigma=$(echo "scale=2; $blur_percent * 0.5" | bc)
        
        output_file="$BLURRED_DIR/${base_name}_blur_${blur_percent}.png"
        
        if [ $blur_percent -eq 0 ]; then
            # Copy original for 0% blur
            cp "$input_file" "$output_file"
        else
            # Apply Gaussian blur
            convert "$input_file" -blur 0x$sigma "$output_file"
        fi
        
        echo "  Created ${blur_percent}% blur (sigma=$sigma)"
    done
}

# Function to analyze sharpness
analyze_sharpness() {
    local base_name=$1
    local results_file="$RESULTS_DIR/${base_name}_sharpness_results.csv"
    
    echo "Analyzing sharpness for $base_name blur progression..."
    echo "blur_percent,filename,sharpness_score,quality_status" > "$results_file"
    
    for blur_percent in {0..10}; do
        local input_file="$BLURRED_DIR/${base_name}_blur_${blur_percent}.png"
        local json_output="$RESULTS_DIR/${base_name}_blur_${blur_percent}_quality.json"
        
        # Run quality validation
        dotnet run --project "$SCRIPT_DIR/../../src/FaceOFFx.Cli" -- \
            validate "$input_file" \
            --sharpness-details \
            > "$json_output" 2>&1 || true
        
        # Extract sharpness score from output
        # Look for "Sharpness:" line in the output
        sharpness_score=$(grep -o "Sharpness:.*" "$json_output" | grep -o "[0-9]\+\.[0-9]\+" | head -1 || echo "0.0")
        
        # Determine quality status
        if (( $(echo "$sharpness_score > 0.5" | bc -l) )); then
            quality_status="PASS"
        else
            quality_status="FAIL"
        fi
        
        echo "$blur_percent,${base_name}_blur_${blur_percent}.png,$sharpness_score,$quality_status" >> "$results_file"
        echo "  Blur ${blur_percent}%: Sharpness=$sharpness_score ($quality_status)"
    done
}

# Function to generate summary report
generate_report() {
    local report_file="$RESULTS_DIR/blur_progression_report.html"
    
    cat > "$report_file" << 'EOF'
<!DOCTYPE html>
<html>
<head>
    <title>FaceOFFx Blur Progression Test Results</title>
    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        .container { max-width: 1200px; margin: 0 auto; }
        .chart-container { width: 100%; height: 400px; margin: 20px 0; }
        .image-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 10px; }
        .image-item { text-align: center; border: 1px solid #ddd; padding: 5px; }
        .pass { border-color: green; }
        .fail { border-color: red; }
        table { border-collapse: collapse; width: 100%; margin: 20px 0; }
        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
        th { background-color: #f2f2f2; }
    </style>
</head>
<body>
    <div class="container">
        <h1>Blur Progression Test Results</h1>
        <p>Testing sharpness detection accuracy across incremental blur levels (0-10%)</p>
EOF

    # Add results for each face
    for results_file in "$RESULTS_DIR"/*_sharpness_results.csv; do
        if [ -f "$results_file" ]; then
            local base_name=$(basename "$results_file" _sharpness_results.csv)
            
            echo "<h2>Results for: $base_name</h2>" >> "$report_file"
            echo "<canvas id='chart_$base_name'></canvas>" >> "$report_file"
            echo "<table>" >> "$report_file"
            echo "<tr><th>Blur %</th><th>Sharpness Score</th><th>Status</th></tr>" >> "$report_file"
            
            # Read CSV and generate table rows
            tail -n +2 "$results_file" | while IFS=, read -r blur_percent filename score status; do
                echo "<tr><td>$blur_percent%</td><td>$score</td><td class='$status'>$status</td></tr>" >> "$report_file"
            done
            
            echo "</table>" >> "$report_file"
        fi
    done

    # Add chart generation script
    cat >> "$report_file" << 'EOF'
    </div>
    <script>
        // Generate charts for each face
EOF

    # Add chart data for each face
    for results_file in "$RESULTS_DIR"/*_sharpness_results.csv; do
        if [ -f "$results_file" ]; then
            local base_name=$(basename "$results_file" _sharpness_results.csv)
            
            echo "        // Chart for $base_name" >> "$report_file"
            echo "        {" >> "$report_file"
            echo "            const ctx = document.getElementById('chart_$base_name').getContext('2d');" >> "$report_file"
            echo "            new Chart(ctx, {" >> "$report_file"
            echo "                type: 'line'," >> "$report_file"
            echo "                data: {" >> "$report_file"
            echo "                    labels: [" >> "$report_file"
            
            # Extract blur percentages
            tail -n +2 "$results_file" | cut -d, -f1 | while read blur; do
                echo -n "'$blur%', " >> "$report_file"
            done
            
            echo "]," >> "$report_file"
            echo "                    datasets: [{" >> "$report_file"
            echo "                        label: 'Sharpness Score'," >> "$report_file"
            echo "                        data: [" >> "$report_file"
            
            # Extract sharpness scores
            tail -n +2 "$results_file" | cut -d, -f3 | while read score; do
                echo -n "$score, " >> "$report_file"
            done
            
            echo "]," >> "$report_file"
            echo "                        borderColor: 'rgb(75, 192, 192)'," >> "$report_file"
            echo "                        tension: 0.1" >> "$report_file"
            echo "                    }]" >> "$report_file"
            echo "                }," >> "$report_file"
            echo "                options: {" >> "$report_file"
            echo "                    scales: {" >> "$report_file"
            echo "                        y: {" >> "$report_file"
            echo "                            beginAtZero: true," >> "$report_file"
            echo "                            max: 1.0" >> "$report_file"
            echo "                        }" >> "$report_file"
            echo "                    }" >> "$report_file"
            echo "                }" >> "$report_file"
            echo "            });" >> "$report_file"
            echo "        }" >> "$report_file"
        fi
    done

    echo "    </script>" >> "$report_file"
    echo "</body>" >> "$report_file"
    echo "</html>" >> "$report_file"
}

# Main execution
echo "FaceOFFx Blur Progression Test"
echo "=============================="

# Process all faces in the faces directory
for face_file in "$FACES_DIR"/*.png; do
    if [ -f "$face_file" ]; then
        base_name=$(basename "$face_file" .png)
        create_blur_progression "$face_file"
        analyze_sharpness "$base_name"
    fi
done

# Generate HTML report
generate_report

echo ""
echo "Test complete! Results:"
echo "- Blurred images: $BLURRED_DIR"
echo "- Analysis results: $RESULTS_DIR"
echo "- HTML report: $RESULTS_DIR/blur_progression_report.html"
echo ""
echo "To view the report, open:"
echo "open $RESULTS_DIR/blur_progression_report.html"