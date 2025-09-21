using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Convolution.Main;

public class ImageProcessor
{
    public void ProcessImage(string inputPath, string outputPath, string kernelName)
    {
        var kernel = Kernels.GetKernelByName(kernelName);

        using var image = Image.Load<Rgb24>(inputPath);

        using var resultImage = ConvolutionAlgorithm.Apply(image, kernel);

        resultImage.Save(outputPath);
        Console.WriteLine($"Result image saved to {outputPath}");
    }
}
