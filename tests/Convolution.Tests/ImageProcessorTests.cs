using Xunit;
using Convolution.Main;
using Convolution.Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;

namespace Convolution.Tests;

public class ImageProcessorTests
{
    private void AssertFilesAreEqual(string expectedPath, string actualPath)
    {
        var expectedBytes = File.ReadAllBytes(expectedPath);
        var actualBytes = File.ReadAllBytes(actualPath);
        Assert.Equal(expectedBytes, actualBytes);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("edgedetection")]
    [InlineData("sharpen")]
    [InlineData("boxblur")]
    [InlineData("zero")]
    [InlineData("sobelx")]
    [InlineData("sobely")]
    [InlineData("laplacian3x3")]
    [InlineData("laplacian5x5")]
    public void ProcessImage_WithValidKernel_CreatesCorrectOutputFile(string kernelName)
    {
        var processor = new ImageProcessor();
        
        string inputPath = Path.GetTempFileName() + ".png";
        string outputPath = Path.GetTempFileName() + ".png";
        string expectedOutputPath = Path.GetTempFileName() + ".png";

        TextWriter originalOut = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            using var sourceImage = ImageGenerator.CreateColorImage(20, 20);
            
            var kernel = Kernels.GetKernelByName(kernelName);
            using var expectedResultImage = ConvolutionAlgorithm.Apply(sourceImage, kernel);
            
            sourceImage.Save(inputPath);
            expectedResultImage.Save(expectedOutputPath);

            processor.ProcessImage(inputPath, outputPath, kernelName);

            Assert.True(File.Exists(outputPath));
            AssertFilesAreEqual(expectedOutputPath, outputPath);
        }
        finally
        {
            Console.SetOut(originalOut);
            File.Delete(inputPath);
            File.Delete(outputPath);
            File.Delete(expectedOutputPath);
        }
    }

    [Fact]
    public void ProcessImage_WithInvalidKernel_ThrowsArgumentException()
    {
        var processor = new ImageProcessor();
        string invalidKernelName = "this_kernel_does_not_exist";

        var exception = Assert.Throws<ArgumentException>(() => 
            processor.ProcessImage("dummy_input.png", "dummy_output.png", invalidKernelName)
        );
        Assert.Contains(invalidKernelName, exception.Message);
        Assert.Contains(" is not known.", exception.Message);
    }
}