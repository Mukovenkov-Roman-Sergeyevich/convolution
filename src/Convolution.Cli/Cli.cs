using CommandLine;
using Convolution.Main;

public class Cli
{
    public class Options
    {
        [Option('i', "input", Required = true, HelpText = "Input image path")]
        public required string InputFile { get; set; }

        [Option('o', "output", Required = true, HelpText = "Output image path")]
        public required string OutputFile { get; set; }

        [Option('k', "kernel", Required = false, Default = "EdgeDetection", 
        HelpText = "Name of kernel\nAvailable: Identity, EdgeDetection, Sharpen, BoxBlur.")]
        public required string Kernel { get; set; }
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
            processor.ProcessImage(opts.InputFile, opts.OutputFile, opts.Kernel);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
        }
    }
}
