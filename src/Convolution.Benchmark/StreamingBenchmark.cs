using BenchmarkDotNet.Attributes;
using Convolution.Common;
using Convolution.Main;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;

namespace Convolution.Benchmarks;

[MemoryDiagnoser]
public class StreamingBenchmark
{
    private const int ImageCount = 10;
    private const int ImageSize = 4096;

    private string _inputDir = null!;
    private string _outputDir = null!;
    private string[] _imagePaths = null!;
    private double[,] _kernel = null!;

    private StreamingImageProcessor _sequentialProcessor = null!;
    private StreamingImageProcessor _parallelProcessor = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _kernel = Kernels.Sharpen;

        _inputDir = Path.Combine(Path.GetTempPath(), "ConvolutionBenchmark_Input");
        _outputDir = Path.Combine(Path.GetTempPath(), "ConvolutionBenchmark_Output");
        Directory.CreateDirectory(_inputDir);

        _imagePaths = new string[ImageCount];
        for (int i = 0; i < ImageCount; i++)
        {
            var path = Path.Combine(_inputDir, $"large_image_{i}.png");
            using var image = ImageGenerator.CreateColorImage(ImageSize, ImageSize);
            image.Save(path);
            _imagePaths[i] = path;
        }

        _sequentialProcessor = new StreamingImageProcessor(ConvolutionAlgorithm.ApplyOptimized);
        _parallelProcessor = new StreamingImageProcessor(ParallelConvolutionAlgorithm.ApplyParallelByRow);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (Directory.Exists(_inputDir)) Directory.Delete(_inputDir, true);
        if (Directory.Exists(_outputDir)) Directory.Delete(_outputDir, true);
    }

    [Benchmark(Description = "Streaming Pipeline (Sequential Convolution)")]
    public void StreamSequential()
    {
        _sequentialProcessor.ProcessStream(_imagePaths, _outputDir, _kernel);
    }

    [Benchmark(Description = "Streaming Pipeline (Parallel Convolution)")]
    public void StreamParallel()
    {
        _parallelProcessor.ProcessStream(_imagePaths, _outputDir, _kernel);
    }
}