using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Exporters;
using Convolution.Main;
using Convolution.Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Convolution.Benchmarks;

[MemoryDiagnoser]
[RPlotExporter]
public class ConvolutionBenchmark
{
    private Image<L8> _sourceGrayscaleImage = null!;
    private Image<Rgb24> _sourceColorImage1 = null!;
    private Image<Rgb24> _sourceColorImage2 = null!;
    private double[,] _kernel = null!;

    [Params("Sharpen", "EdgeDetection", "BoxBlur")]
    public string KernelName = null!;

    [Params(256, 512, 1024, 2048)]
    public int ImageSize;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _kernel = KernelName.ToLowerInvariant() switch
        {
            "sharpen" => Kernels.Sharpen,
            "edgedetection" => Kernels.EdgeDetection,
            "boxblur" => Kernels.BoxBlur,
            _ => Kernels.Identity
        };

        _sourceGrayscaleImage = ImageGenerator.CreateGrayscaleImage(ImageSize, ImageSize);
        _sourceColorImage1 = ImageGenerator.CreateColorImage(ImageSize, ImageSize);
        _sourceColorImage2 = ImageGenerator.CreateColorImage(ImageSize, ImageSize);
    }
    
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _sourceGrayscaleImage.Dispose();
        _sourceColorImage1.Dispose();
        _sourceColorImage2.Dispose();
    }

    [Benchmark(Description = "Sequential Convolution (Grayscale)")]
    public void SequentialConvolutionGrayscale()
    {
        using var result = ConvolutionAlgorithm.Apply(_sourceGrayscaleImage, _kernel);
    }

    [Benchmark(Description = "Sequential Convolution (Color)")]
    public void SequentialConvolutionColor()
    {
        using var result = ConvolutionAlgorithm.Apply(_sourceColorImage1, _kernel);
    }

    [Benchmark(Description = "Sequential Convolution Optimized (Color)")]
    public void SequentialConvolutionOptimziedColor()
    {
        using var result = ConvolutionAlgorithm.ApplyOptimized(_sourceColorImage2, _kernel);
    }
}
