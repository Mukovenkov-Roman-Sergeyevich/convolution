using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Concurrent;

namespace Convolution.Main;

public delegate Image<Rgb24> ConvolutionFunc(Image<Rgb24> source, double[,] kernel);

public class StreamingImageProcessor
{
    private readonly ConvolutionFunc _convolutionFunc;
    private readonly int _boundedCapacity;
    private readonly int _concurrencyLevel;

    private record ImageData(Image<Rgb24> Image, string OriginalPath);

    public StreamingImageProcessor(ConvolutionFunc convolutionFunc, int boundedCapacity = 4, int concurrencyLevel = 0)
    {
        _convolutionFunc = convolutionFunc;
        _boundedCapacity = boundedCapacity;
        _concurrencyLevel = concurrencyLevel > 0 ? concurrencyLevel : Environment.ProcessorCount;
    }

    public void ProcessStream(IEnumerable<string> inputPaths, string outputDirectory, double[,] kernel)
    {
        using var readBuffer = new BlockingCollection<ImageData>(_boundedCapacity);
        using var processedBuffer = new BlockingCollection<ImageData>(_boundedCapacity);

        var readTask = Task.Run(() =>
        {
            try
            {
                foreach (var path in inputPaths)
                {
                    var image = Image.Load<Rgb24>(path);
                    readBuffer.Add(new ImageData(image, path));
                }
            }
            finally
            {
                readBuffer.CompleteAdding();
            }
        });

        var convolveTasks = Enumerable.Range(0, _concurrencyLevel).Select(_ => Task.Run(() =>
        {
            foreach (var data in readBuffer.GetConsumingEnumerable())
            {
                using (data.Image)
                {
                    var resultImage = _convolutionFunc(data.Image, kernel);
                    processedBuffer.Add(new ImageData(resultImage, data.OriginalPath));
                }
            }
        })).ToArray();

        var allConvolveTasks = Task.WhenAll(convolveTasks).ContinueWith(_ =>
        {
            processedBuffer.CompleteAdding();
        });

        var writeTask = Task.Run(() =>
        {
            Directory.CreateDirectory(outputDirectory);
            foreach (var data in processedBuffer.GetConsumingEnumerable())
            {
                using (data.Image)
                {
                    var fileName = Path.GetFileName(data.OriginalPath);
                    var outputPath = Path.Combine(outputDirectory, fileName);
                    data.Image.Save(outputPath);
                }
            }
        });

        Task.WaitAll(readTask, allConvolveTasks, writeTask);
    }
}