using System.Numerics;
using System.Text.RegularExpressions;

namespace SmallAreaFlowComp;

public partial class FileProcessor
{
    // Version Variables
    const string FlowCompScriptVer = "V0.7.1";
    const string FlowModelVer = "V0.2.1";
    const string ErrorLoggerVer = "V0.0.1";

    // Flags that are checked for in slicer gcode (for ares to modify)
    private static readonly string[] slicerInfillFlags = { ";TYPE:Solid infill",
            ";TYPE:Top solid infill",
            ";TYPE:Internal solid infill",
            ";TYPE:Top surface",
            ";TYPE:Bottom surface",
            "; FEATURE: Top surface",
            "; FEATURE: Internal solid infill",
            "; FEATURE: Bottom surface"};

    // Flags that are checked for change in extrusion type
    private static readonly string[] slicerGenericFlags = { ";TYPE:", "; FEATURE:" };

    // Regex pattern to check if gcode lines and segments
    private static readonly Regex GcodeLineOfInterest = CoordinateAndNumber(); // Contains either XYZ followed by number
    private static readonly Regex ExtrusionMovePattern = ExtrusionAndNumber(); // E Followed by a non negative decimal
    
    private readonly IFlowMaths _flowMaths;
    private readonly TextReader _inputStream;
    private readonly TextWriter _outputStream;
    private readonly ILogger _logger;

    public FileProcessor(IFlowMaths flowMaths, TextReader inputStream, TextWriter outputStream, ILogger logger)
    {
        _flowMaths = flowMaths;
        _inputStream = inputStream;
        _outputStream = outputStream;
        _logger = logger;
    }

    public async Task ProcessFile()
    {
        // Start Reading GCode File
        try
        {
            // Add header to temp gcode file
            await WriteGCodeHeader(_outputStream.WriteLine);

            var state = new GCodeProcessingState
            {
                AdjustingFlow = false,
                ExtrusionLength = -1f,
                PreviousToolPos = Vector2.Zero
            };

            string? line;
            while ((line = _inputStream.ReadLine()) != null)
            {
                ProcessLine(line, state);
            }
        }

        catch (Exception ex)
        {
            _logger.Fatal($"An error occurred: {ex.Message}");
            Environment.Exit(999);
        }
    }

    private void ProcessLine(string line, GCodeProcessingState state)
    {
        // Check if gcode file has alread been parsed
        if (line.Trim() == "; File Parsed By Flow Comp Script")
            Environment.Exit(1);

        // Check if reading gcode that needs flow comp (see flags above)
        if (slicerInfillFlags.Contains(line))
        {
            state.AdjustingFlow = true;
        }
        else if (state.AdjustingFlow)
        {
            foreach (var genericFlag in slicerGenericFlags)
            {
                if (line.Contains(genericFlag))
                    state.AdjustingFlow = false;
            }
        }

        // Update current tool position
        var currentToolPos = _flowMaths.UpdateToolPos(line, state.PreviousToolPos);

        if (state.AdjustingFlow && GcodeLineOfInterest.IsMatch(line))
        {
            double oldFlowVal = -1f;
            double newFlowVal = -1f;

            // Break GCodeLine Into It's Segments
            string[] gcodeLineSegments = line.Trim().Split(' ');

            // Loop through each segment of gcode
            for (int i = 0; i < gcodeLineSegments.Length; i++)
            {
                // Check if the segment is an extrusion move we want to modify
                if (ExtrusionMovePattern.IsMatch(gcodeLineSegments[i]))
                {
                    // Try convert e value and update newFlowVal and oldFlowVal
                    if (double.TryParse(gcodeLineSegments[i][1..], out oldFlowVal))
                    {
                        state.ExtrusionLength = _flowMaths.CalcExtrusionLength(currentToolPos, state.PreviousToolPos);

                        if (state.ExtrusionLength < _flowMaths.maxModifiedLength() && state.ExtrusionLength > 0)
                        {
                            newFlowVal = _flowMaths.ModifyFlow(state.ExtrusionLength, oldFlowVal);
                            gcodeLineSegments[i] = "E" + newFlowVal.ToString("N5");
                        }
                    }
                    else
                        _logger.Error($"Unable to convert {gcodeLineSegments[i][1..]} to double");
                }
            }

            // Modify E value if it's been changed (doesn't modify retraction stuff)
            if (oldFlowVal > 0 && oldFlowVal != newFlowVal)
                line = string.Join(' ', gcodeLineSegments) + $"; Old Flow Value: {oldFlowVal}   Length: {state.ExtrusionLength:N5}";
        }
        state.PreviousToolPos = currentToolPos;

        // Write GCode Line (modifed or not) to temp gcode file                    
        _outputStream.WriteLine(line);
    }

    // Create header for top of gcode file
    private static Task WriteGCodeHeader(Action<string> writer) =>
        Task.Run(() =>
        {
            writer("; File Parsed By Flow Comp Script");
            writer($"; Script Ver. {FlowCompScriptVer}");
            writer($"; Flow Model Ver. {FlowModelVer}");
            writer($"; Logger Ver. {ErrorLoggerVer}");
        });

    [GeneratedRegex("^E(?:0\\.\\d+|\\.\\d+|[1-9]\\d*|\\d+\\.\\d+)$")]
    private static partial Regex ExtrusionAndNumber();
    [GeneratedRegex("[XYE][\\d]+(?:\\.\\d+)?")]
    private static partial Regex CoordinateAndNumber();
}

public class GCodeProcessingState
{
    public bool AdjustingFlow { get; set; }
    public Vector2 PreviousToolPos { get; set; }
    public double ExtrusionLength { get; set; }
}