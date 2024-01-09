// Error Codes:                                 //
// 1 - Fed GCode Already Parsed                 //
// 2 - Unable to make/edit temp gcode file      //
// 3 - Wrong format E Lengths in model.txt      //
// 4 - Wrong format Flow Comp in model.txt      //
// 5 - Issue loading model.txt file             //
// 6 - Issue with line parts count in model.txt //
// 7 - First/Last flow model values incorrect   //
// 8 - Not enough flow model points (min. 3)    //
// 9 - Flow comp applied to negative number     //
// 10 - Issue creating model.txt file           //
//                                              //
// 999 - Check Log, Unknown Error               //

using System.Text;
using CommandLine;

namespace SmallAreaFlowComp;

public class Program
{
    public class Options
    {
        [Option('i', "input-file", Required = false, Group = "Input/Output", HelpText = "Set the input file to read from, defaults to STDIN if not provided.")]
        public string? InputFilename { get; set; }

        [Option('o', "output-file", Required = false, Group = "Input/Output", HelpText = "Set the output file to write to, defaults to STDOUT if not provided.")]
        public string? OutputFilename { get; set; }

        [Option('l', "log-file", Required = false, Group = "Input/Output", HelpText = "Set the logfile for program output, defaults to STDERR if not provided.")]
        public string? LogFilename { get; set; }

        [Option("loglevel", Default = LogLevel.Information , Required = false, Group = "Advanced", HelpText = "Set the allowable log level (none, fatal, error, warning, information, debug)")]
        public LogLevel LogLevel { get; set; }
    }

    public static async Task Main(string[] args)
    {
        // Forces script to interpert , and . as thousands seperator and decimal respectively
        Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

        // Make sure the script is aware of its current directory
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

        var input = TextReader.Null;
        var output = TextWriter.Null;
        var logger = SimpleLogger.Null;

        var parser = new Parser(cfg =>
        {
            cfg.CaseInsensitiveEnumValues = true;
        });

        parser.ParseArguments<Options>(args)
            .WithParsed(o =>
            {
                input = PrepareInput(o.InputFilename);
                output = PrepareOutput(o.OutputFilename);
                logger = PrepareLogger(o.LogFilename, o.LogLevel);
            });

        var flowMaths = new FlowMaths(logger);
        var processor = new FileProcessor(
            flowMaths,
            input,
            output,
            logger
        );

        await processor.ProcessFile();
    }

    private static ILogger PrepareLogger(string? filename, LogLevel logLevel)
    {
        var logOutput = string.IsNullOrEmpty(filename)
            ? Console.Error
            : File.AppendText(filename);

        return new SimpleLogger(logOutput, logLevel);
    }

    private static TextReader PrepareInput(string? filename) =>
        string.IsNullOrEmpty(filename)
            ? Console.In
            : File.OpenText(filename);

    private static TextWriter PrepareOutput(string? filename) =>
        string.IsNullOrEmpty(filename)
            ? Console.Out
            : File.CreateText(filename);
}