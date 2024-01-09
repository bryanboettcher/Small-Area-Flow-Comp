using System.Numerics;
using System.Text.RegularExpressions;
using MathNet.Numerics.Interpolation;
using SmallAreaFlowComp;

// Class to handle all the flow maths etc.
public interface IFlowMaths
{
    void InitializeModel();
    double maxModifiedLength();
    void OutputFlowModelParameters(Action<string> writer);
    double ModifyFlow(double extrusionLength, double eValue);
    double CalcExtrusionLength(Vector2 endPos, Vector2 startPos);
    Vector2 UpdateToolPos(string gcodeLine, Vector2 previousToolPos);
}

public class FlowMaths : IFlowMaths
{
    private readonly ILogger _logger;
    private List<double> eLengths = new(), flowComps = new();
    private CubicSpline flowModel;
    private string[] defaultModel = {"0, 0",
        "0.2, 0.4444",
        "0.4, 0.6145",
        "0.6, 0.7059",
        "0.8, 0.7619",
        "1.5, 0.8571",
        "2, 0.8889",
        "3, 0.9231",
        "5, 0.9520",
        "10, 1"};

    // Constructor for flow maths
    public FlowMaths(ILogger logger)
    {
        _logger = logger;
    }

    public void InitializeModel()
    {
        string eLengthPattern = @"^\d+(\.\d+)?$";
        string flowCompPattern = @"^(0(\.\d+)?|1(\.0+)?)$";

        string modelParametersPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "model.txt");

        try
        {
            // Create a model.txt file if it doesn't exist
            if (!File.Exists(modelParametersPath))
                createModel(modelParametersPath);

            using (StreamReader reader = new(modelParametersPath))
            {
                string? modelParameterLine;
                while ((modelParameterLine = reader.ReadLine()) != null)
                {
                    _logger.Debug(modelParameterLine);
                    string[] lineParts = modelParameterLine.Trim().Split(',');

                    // Check line has right amount of parts
                    if (lineParts.Length > 2 || lineParts.Length == 0)
                    {
                        _logger.Fatal("Incorrect format of parameter line in model.txt");
                        Environment.Exit(6);
                    }

                    if (Regex.IsMatch(lineParts[0].Trim(), eLengthPattern))
                        eLengths.Add(Convert.ToDouble(lineParts[0].Trim()));
                    else
                    {
                        _logger.Fatal($"Incorrect format of eLength in model.txt: {lineParts[0].Trim()}");
                        Environment.Exit(3);
                    }

                    if (Regex.IsMatch(lineParts[1].Trim(), flowCompPattern))
                        flowComps.Add(Convert.ToDouble(lineParts[1].Trim()));
                    else
                    {
                        _logger.Fatal($"Incorrect format of flowComp in model.txt: {lineParts[1].Trim()}");
                        Environment.Exit(4);
                    }
                }
            }

            if (eLengths[0] != 0.0 || flowComps[^1] != 1.0f)
            {
                _logger.Fatal("First E length must be 0.0 and last flowComp must be 1.0");
                Environment.Exit(7);
            }

            if (eLengths.Count() < 3)
            {
                _logger.Fatal("Please specifiy atleast 3 flow model points");
                Environment.Exit(8);
            }

        }

        catch (Exception ex)
        {
            _logger.Fatal($"An error occured: {ex.Message}");
            Environment.Exit(999);
        }

        flowModel = CubicSpline.InterpolateNatural(eLengths, flowComps);
    }

    // Creates a default model.txt file
    private void createModel(string directory)
    {
        try
        {
            _logger.Warning("Creating model.txt file (as one doesn't exist)");
            using (StreamWriter writer = File.AppendText(directory))
            {
                foreach(string modelPoint in defaultModel)
                {
                    writer.WriteLine(modelPoint);
                }
            }
            _logger.Information("Succesfully created model.txt file");
        }
        catch (Exception ex)
        {
            _logger.Fatal($"Error with creating model.txt file: {ex.Message}");
            Environment.Exit(10);
        }
    }

    // Getter for longest length in eLengths
    public double maxModifiedLength()
    {
        return eLengths[^1];
    }

    // Returns the parameters used by the flow model
    public void OutputFlowModelParameters(Action<string> writer)
    {
        writer("; Flow Comp Model Points:");
        for(var index = 0; index < eLengths.Count(); index++) 
            writer($"; ({eLengths[index]}, {flowComps[index]})");
        writer(string.Empty);
    }

    // Returns flow multiplier value from flow model
    private double flowCompModel(double extrusionLength)
    {
        if(extrusionLength < 0.0)
        {
            _logger.Warning("Tried to apply flow comp to extrusion length < 0");
            return 1;
        }

        if(extrusionLength > eLengths[^1])
        {
            _logger.Warning($"Tried to apply flow comp to extrusion length > max flow comp length: {eLengths[^1]}");
            return 1;
        }

        return flowModel.Interpolate(extrusionLength);
    }

    // Applies flow compensation model
    public double ModifyFlow(double extrusionLength, double eValue)
    {
        return Math.Round(eValue * flowCompModel(extrusionLength), 5);
    }

    // Returns double value of distance between two Vector2's (endPos, startPos)
    public double CalcExtrusionLength(Vector2 endPos, Vector2 startPos)
    {
        return Vector2.Distance(endPos, startPos);
    }

    // Create a 2D Vector for toolhead XY position from GCode Line
    // If gcodeLine doesn't contain an X or Y coord, it's flagged as -999
    private Vector2 vectorPos(string gcodeLine)
    {
        float xPos = -999, yPos = -999;

        // Check gcode line isn't a blank line
        if (gcodeLine.Length != 0)
        {
            // Check the gcode line isn't just a comment
            if (gcodeLine[0] != ';')
            {
                string[] gcodeSegments = gcodeLine.Trim().Split(' ');

                // Go through each segment of the gcode line
                foreach (string segment in gcodeSegments)
                {
                    try{
                        // Check segment isn't blank
                        if(segment.Length != 0)
                        {
                            // Try update X coordinate
                            if (segment[0] == 'X' || segment[0] == 'x')
                                xPos = (float)Convert.ToDouble(segment.Substring(1));

                            // Try update Y coordinate
                            else if (segment[0] == 'Y' || segment[0] == 'y')
                                yPos = (float)Convert.ToDouble(segment.Substring(1));
                            
                            // If it finds a comment, stops reading the segments
                            else if(segment[0] == ';')
                                break;
                        }
                    }

                    catch(Exception ex)
                    {
                        _logger.Error($"Tried converting {segment.Substring(1)} to double but got error: {ex.Message}");
                    }
                }
            }
        }

        return new(xPos, yPos);
    }

    // Creates a 2D Vector for toolhead's new XY position from a GCode Line
    public Vector2 UpdateToolPos(string gcodeLine, Vector2 previousToolPos)
    {
        Vector2 toolPos = vectorPos(gcodeLine);

        if (toolPos.X == -999)
            toolPos.X = previousToolPos.X;

        if (toolPos.Y == -999)
            toolPos.Y = previousToolPos.Y;

        return toolPos;
    }
}