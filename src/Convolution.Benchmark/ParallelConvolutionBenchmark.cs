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
public class ParallelConvolutionBenchmark
{
    private Image<Rgb24> _sourceColorImage = null!;
    private double[,] _kernel = null!;

    [Params("Sharpen", "BoxBlur")]
    public string KernelName = null!;

    [Params(512, 1024, 2048)]
    public int ImageSize;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _kernel = KernelName switch
        {
            "Sharpen" => Kernels.Sharpen,
            _ => Kernels.BoxBlur
        };
        _sourceColorImage = ImageGenerator.CreateColorImage(ImageSize, ImageSize);
    }
    
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _sourceColorImage.Dispose();
    }

    [Benchmark(Description = "Sequential Optimized (Baseline)", Baseline = true)]
    public void SequentialOptimized()
    {
        using var result = ConvolutionAlgorithm.ApplyOptimized(_sourceColorImage, _kernel);
    }

    [Benchmark(Description = "Parallel by Row")]
    public void ParallelByRow()
    {
        using var result = ParallelConvolutionAlgorithm.ApplyParallelByRow(_sourceColorImage, _kernel);
    }

    [Benchmark(Description = "Parallel by Column")]
    public void ParallelByColumn()
    {
        using var result = ParallelConvolutionAlgorithm.ApplyParallelByColumn(_sourceColorImage, _kernel);
    }

    [Benchmark(Description = "Parallel by Tile")]
    public void ParallelByTile()
    {
        using var result = ParallelConvolutionAlgorithm.ApplyParallelByTile(_sourceColorImage, _kernel);
    }

    [Benchmark(Description = "Parallel by Pixel")]
    public void ParallelByPixel()
    {
        using var result = ParallelConvolutionAlgorithm.ApplyParallelByPixel(_sourceColorImage, _kernel);
    }
}