using BenchmarkDotNet.Running;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Attributes;

namespace Convolution.Benchmarks;

[MemoryDiagnoser]
[RPlotExporter]
public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}