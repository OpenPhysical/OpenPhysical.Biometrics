using JetBrains.Annotations;
using OpenPhysical.Biometrics.Core.Abstractions.Pipeline;
using OpenPhysical.Biometrics.Core.Domain.Detection;
using Spectre.Console;
using System.Collections.Immutable;

namespace OpenPhysical.Biometrics.Infrastructure.Pipeline.Checkpoints;

/// <summary>
/// Base checkpoint for face selection when multiple faces are detected.
/// </summary>
[PublicAPI]
public abstract class FaceSelectionCheckpoint : ICheckpoint<IReadOnlyList<DetectedFace>>
{
    protected readonly ILogger<FaceSelectionCheckpoint> _logger;

    /// <summary>
    /// Gets the unique identifier for this checkpoint.
    /// </summary>
    public string CheckpointId => "face-selection";

    /// <summary>
    /// Gets the human-readable name of this checkpoint.
    /// </summary>
    public string Name => "Face Selection";

    /// <summary>
    /// Gets the description of the decision required at this checkpoint.
    /// </summary>
    public string Description => "Select the primary face for biometric processing when multiple faces are detected";

    /// <summary>
    /// Gets the available decision options for this checkpoint.
    /// </summary>
    public ImmutableList<CheckpointOption> AvailableOptions { get; } = ImmutableList.Create(
        new CheckpointOption(
            "select-face", 
            "Select Face", 
            "Choose a specific face for processing",
            CheckpointOptionType.ContinueWithModifications
        ),
        new CheckpointOption(
            "select-highest-confidence", 
            "Highest Confidence", 
            "Automatically select face with highest confidence",
            CheckpointOptionType.Continue
        ),
        new CheckpointOption(
            "select-largest", 
            "Largest Face", 
            "Automatically select the largest detected face",
            CheckpointOptionType.Continue
        ),
        new CheckpointOption(
            "reject-multiple", 
            "Reject Multiple Faces", 
            "Reject image due to multiple faces",
            CheckpointOptionType.Abort
        )
    );

    /// <summary>
    /// Initializes a new instance of the face selection checkpoint.
    /// </summary>
    protected FaceSelectionCheckpoint(ILogger<FaceSelectionCheckpoint> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Requests a human decision for face selection.
    /// </summary>
    public async Task<Result<CheckpointResult>> RequestDecisionAsync(
        IReadOnlyList<DetectedFace> data,
        IPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Face selection checkpoint reached: {FaceCount} faces detected for execution {ExecutionId}", 
            data.Count, context.ExecutionId);

        try
        {
            // If only one face, no decision needed
            if (data.Count == 1)
            {
                _logger.LogDebug("Single face detected, automatically continuing");
                return Result.Success(CheckpointResult.Continue("select-highest-confidence"));
            }

            // If no faces, abort
            if (data.Count == 0)
            {
                _logger.LogWarning("No faces detected for selection");
                return Result.Success(CheckpointResult.Abort("reject-multiple", "No faces detected"));
            }

            var decision = await RequestFaceSelectionAsync(data, context, cancellationToken);
            
            _logger.LogInformation(
                "Face selection decision: {Decision} for execution {ExecutionId}",
                decision.Decision, context.ExecutionId);

            return Result.Success(decision);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Face selection checkpoint was cancelled");
            return Result.Failure<CheckpointResult>("Face selection was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Face selection checkpoint failed");
            return Result.Failure<CheckpointResult>($"Face selection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Abstract method for implementation-specific face selection logic.
    /// </summary>
    protected abstract Task<CheckpointResult> RequestFaceSelectionAsync(
        IReadOnlyList<DetectedFace> detectedFaces,
        IPipelineContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Helper method to format face information for display.
    /// </summary>
    protected static string FormatFaceInfo(DetectedFace face, int index)
    {
        var area = face.BoundingBox.Width * face.BoundingBox.Height;
        return $"Face {index + 1}: Confidence={face.Confidence:F3}, " +
               $"Size={face.BoundingBox.Width}×{face.BoundingBox.Height} " +
               $"(Area={area:N0}px), " +
               $"Position=({face.BoundingBox.X},{face.BoundingBox.Y})";
    }

    /// <summary>
    /// Automatically selects face based on specified criteria.
    /// </summary>
    protected static DetectedFace SelectFaceAutomatically(IReadOnlyList<DetectedFace> faces, string criteria)
    {
        return criteria switch
        {
            "highest-confidence" => faces.OrderByDescending(f => f.Confidence).First(),
            "largest" => faces.OrderByDescending(f => f.BoundingBox.Width * f.BoundingBox.Height).First(),
            _ => faces.OrderByDescending(f => f.Confidence).First() // Default to highest confidence
        };
    }
}

/// <summary>
/// Console-based implementation of face selection checkpoint.
/// </summary>
[PublicAPI]
public sealed class ConsoleFaceSelectionCheckpoint : FaceSelectionCheckpoint
{
    private readonly IAnsiConsole _console;

    /// <summary>
    /// Initializes a new instance of the console face selection checkpoint.
    /// </summary>
    public ConsoleFaceSelectionCheckpoint(
        IAnsiConsole console,
        ILogger<FaceSelectionCheckpoint> logger) : base(logger)
    {
        _console = console;
    }

    /// <summary>
    /// Requests face selection via console interaction.
    /// </summary>
    protected override async Task<CheckpointResult> RequestFaceSelectionAsync(
        IReadOnlyList<DetectedFace> detectedFaces,
        IPipelineContext context,
        CancellationToken cancellationToken)
    {
        // Display detected faces
        _console.MarkupLine($"[bold]Multiple Faces Detected ({detectedFaces.Count} faces)[/]");
        _console.WriteLine();

        var table = new Table()
            .AddColumn("Face")
            .AddColumn("Confidence")
            .AddColumn("Size")
            .AddColumn("Position");

        for (int i = 0; i < detectedFaces.Count; i++)
        {
            var face = detectedFaces[i];
            var area = face.BoundingBox.Width * face.BoundingBox.Height;
            
            table.AddRow(
                $"Face {i + 1}",
                $"{face.Confidence:F3}",
                $"{face.BoundingBox.Width}×{face.BoundingBox.Height} ({area:N0}px)",
                $"({face.BoundingBox.X}, {face.BoundingBox.Y})"
            );
        }

        _console.Write(table);
        _console.WriteLine();

        // Prompt for selection strategy
        var prompt = new SelectionPrompt<string>()
            .Title("How would you like to select the face for processing?");
        prompt.AddChoices(new[] {
            "select-face: Manually select a specific face",
            $"select-highest-confidence: Use highest confidence face (Face {GetHighestConfidenceIndex(detectedFaces) + 1})",
            $"select-largest: Use largest face (Face {GetLargestFaceIndex(detectedFaces) + 1})",
            "reject-multiple: Reject image due to multiple faces"
        });
        
        var choice = _console.Prompt(prompt);

        return choice switch
        {
            var s when s.StartsWith("select-face:") => await RequestSpecificFace(detectedFaces),
            var s when s.StartsWith("select-highest-confidence:") => SelectHighestConfidenceFace(detectedFaces),
            var s when s.StartsWith("select-largest:") => SelectLargestFace(detectedFaces),
            var s when s.StartsWith("reject-multiple:") => await RequestRejectionReason(),
            _ => CheckpointResult.Abort("reject-multiple", "Invalid choice")
        };
    }

    /// <summary>
    /// Requests user to select a specific face.
    /// </summary>
    private async Task<CheckpointResult> RequestSpecificFace(IReadOnlyList<DetectedFace> faces)
    {
        var faceChoices = faces
            .Select((face, index) => $"face-{index}")
            .ToArray();

        var faceDescriptions = faces
            .Select((face, index) => FormatFaceInfo(face, index))
            .ToArray();

        var prompt = new SelectionPrompt<string>()
            .Title("Select the face to use for processing:");

        for (int i = 0; i < faceChoices.Length; i++)
        {
            prompt.AddChoice($"{faceChoices[i]}: {faceDescriptions[i]}");
        }

        var selectedChoice = _console.Prompt(prompt);
        var selectedIndex = int.Parse(selectedChoice.Split('-')[1]);
        var selectedFace = faces[selectedIndex];

        var selectionData = new { SelectedFaceIndex = selectedIndex, SelectedFace = selectedFace };
        return CheckpointResult.ContinueWithModifications(
            "select-face", 
            selectionData, 
            $"Selected face {selectedIndex + 1} with confidence {selectedFace.Confidence:F3}");
    }

    /// <summary>
    /// Selects face with highest confidence.
    /// </summary>
    private CheckpointResult SelectHighestConfidenceFace(IReadOnlyList<DetectedFace> faces)
    {
        var selectedFace = SelectFaceAutomatically(faces, "highest-confidence");
        var selectedIndex = faces.ToList().IndexOf(selectedFace);

        var selectionData = new { SelectedFaceIndex = selectedIndex, SelectedFace = selectedFace };
        return CheckpointResult.ContinueWithModifications(
            "select-highest-confidence", 
            selectionData, 
            $"Auto-selected face with highest confidence: {selectedFace.Confidence:F3}");
    }

    /// <summary>
    /// Selects largest face by area.
    /// </summary>
    private CheckpointResult SelectLargestFace(IReadOnlyList<DetectedFace> faces)
    {
        var selectedFace = SelectFaceAutomatically(faces, "largest");
        var selectedIndex = faces.ToList().IndexOf(selectedFace);

        var selectionData = new { SelectedFaceIndex = selectedIndex, SelectedFace = selectedFace };
        return CheckpointResult.ContinueWithModifications(
            "select-largest", 
            selectionData, 
            $"Auto-selected largest face with area {selectedFace.BoundingBox.Width * selectedFace.BoundingBox.Height:N0}px");
    }

    /// <summary>
    /// Requests rejection reason from user.
    /// </summary>
    private async Task<CheckpointResult> RequestRejectionReason()
    {
        var reason = _console.Prompt(
            new TextPrompt<string>("Enter reason for rejecting multiple faces:")
                .DefaultValue("Multiple faces not allowed for this processing type")
        );

        return CheckpointResult.Abort("reject-multiple", reason);
    }

    /// <summary>
    /// Gets index of face with highest confidence.
    /// </summary>
    private static int GetHighestConfidenceIndex(IReadOnlyList<DetectedFace> faces)
    {
        var maxConfidence = faces.Max(f => f.Confidence);
        return faces.ToList().FindIndex(f => Math.Abs(f.Confidence - maxConfidence) < 0.001f);
    }

    /// <summary>
    /// Gets index of largest face by area.
    /// </summary>
    private static int GetLargestFaceIndex(IReadOnlyList<DetectedFace> faces)
    {
        var maxArea = faces.Max(f => f.BoundingBox.Width * f.BoundingBox.Height);
        return faces.ToList().FindIndex(f => f.BoundingBox.Width * f.BoundingBox.Height == maxArea);
    }
}