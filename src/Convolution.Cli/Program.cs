using CommandLine;
using Convolution.Main;

public class Program
{
    public class Options
    {
        [Option('i', "input", Required = true, HelpText = "Input image path")]
        public required string InputFile { get; set; }

        [Option('o', "output", Required = true, HelpText = "Output image path")]
        public required string OutputFile { get; set; }
    }

    public static void Main(string[] args)
    {
        Parser.Default.ParseArguments<Options>(args).WithParsed(RunOptions);
    }

    static void RunOptions(Options opts)
    {
        try
        {
            var processor = new ImageProcessor();
            processor.ApplyGrayscale(opts.InputFile, opts.OutputFile);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}