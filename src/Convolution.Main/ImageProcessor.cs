using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Convolution.Main;

public class ImageProcessor
{
    public void ApplyGrayscale(string inputPath, string outputPath)
    {
        using var image = Image.Load(inputPath);

        image.Mutate(x => x.Grayscale());

        image.Save(outputPath);
        Console.WriteLine($"Image saved to {outputPath}");
    }
}
