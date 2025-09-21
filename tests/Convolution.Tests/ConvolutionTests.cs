using Convolution.Main;
using Convolution.Common;
using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xunit;

namespace Convolution.Tests;

public class ConvolutionTests
{

    // Auxilary

    private static void AssertImagesAreEqual(Image<L8> expected, Image<L8> actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);

        for (int y = 0; y < expected.Height; y++)
        {
            for (int x = 0; x < expected.Width; x++)
            {
                Assert.True(expected[x, y].PackedValue == actual[x, y].PackedValue,
                    $"Grayscale pixel mismatch at ({x},{y}). Expected: {expected[x, y].PackedValue}, Actual: {actual[x, y].PackedValue}");
            }
        }
    }

    private static void AssertImagesAreEqual(Image<Rgb24> expected, Image<Rgb24> actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);

        for (int y = 0; y < expected.Height; y++)
        {
            for (int x = 0; x < expected.Width; x++)
            {
                var p1 = expected[x, y];
                var p2 = actual[x, y];
                Assert.True(p1.R == p2.R && p1.G == p2.G && p1.B == p2.B,
                    $"Color pixel mismatch at ({x},{y}). Expected: {p1}, Actual: {p2}");
            }
        }
    }

    private static void AssertImagesAreSimilar(Image<L8> expected, Image<L8> actual, double maxAcceptableMae = 0.2)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);

        long totalAbsoluteError = 0;
        int totalPixels = expected.Width * expected.Height;

        for (int y = 0; y < expected.Height; y++)
        {
            for (int x = 0; x < expected.Width; x++)
            {
                int error = Math.Abs(expected[x, y].PackedValue - actual[x, y].PackedValue);
                totalAbsoluteError += error;
            }
        }

        double mae = (double)totalAbsoluteError / totalPixels;

        Assert.True(mae <= maxAcceptableMae, 
            $"Mean Absolute Error is too high: {mae}. Expected <= {maxAcceptableMae}");
    }


    private static void AssertImagesAreSimilar(Image<Rgb24> expected, Image<Rgb24> actual, double maxAcceptableMae = 0.2)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);

        long totalAbsoluteError = 0;
        int totalPixels = expected.Width * expected.Height;

        for (int y = 0; y < expected.Height; y++)
        {
            for (int x = 0; x < expected.Width; x++)
            {
                var p1 = expected[x, y];
                var p2 = actual[x, y];
                
                int errorR = Math.Abs(p1.R - p2.R);
                int errorG = Math.Abs(p1.G - p2.G);
                int errorB = Math.Abs(p1.B - p2.B);
                
                int pixelError = Math.Max(errorR, Math.Max(errorG, errorB));
                totalAbsoluteError += pixelError;

            }
        }
        
        double mae = (double)totalAbsoluteError / totalPixels;

        Assert.True(mae <= maxAcceptableMae, 
            $"Mean Absolute Error for color image is too high: {mae}. Expected <= {maxAcceptableMae}");
    }

    private static double[,] ComposeKernels(double[,] kernelA, double[,] kernelB)
    {
        int aHeight = kernelA.GetLength(0); int aWidth = kernelA.GetLength(1);
        int bHeight = kernelB.GetLength(0); int bWidth = kernelB.GetLength(1);
        int newHeight = aHeight + bHeight - 1; int newWidth = aWidth + bWidth - 1;
        var resultKernel = new double[newHeight, newWidth];
        for (int y = 0; y < newHeight; y++)
        for (int x = 0; x < newWidth; x++) {
            double sum = 0;
            for (int j = 0; j < aHeight; j++)
            for (int i = 0; i < aWidth; i++)
                if (x - i >= 0 && x - i < bWidth && y - j >= 0 && y - j < bHeight)
                    sum += kernelA[j, i] * kernelB[y - j, x - i];
            resultKernel[y, x] = sum;
        }
        return resultKernel;
    }

    private static void AssertKernelsAreSimilar(double[,] expected, double[,] actual, double tolerance = 1e-9)
    {
        Assert.Equal(expected.GetLength(0), actual.GetLength(0));
        Assert.Equal(expected.GetLength(1), actual.GetLength(1));

        for (int y = 0; y < expected.GetLength(0); y++)
        {
            for (int x = 0; x < expected.GetLength(1); x++)
            {
                Assert.True(Math.Abs(expected[y, x] - actual[y, x]) < tolerance,
                    $"Kernel mismatch at ({y},{x}). Expected: {expected[y, x]}, Actual: {actual[y, x]}");
            }
        }
    }

    private static Image<T> GetMagickNetResult<T>(Image<T> sourceImage, double[,] kernel)
        where T : unmanaged, SixLabors.ImageSharp.PixelFormats.IPixel<T>
    {
        int order = kernel.GetLength(1);
        var magickKernel = new ConvolveMatrix((uint)order);
        for (int y = 0; y < kernel.GetLength(0); y++)
        {
            for (int x = 0; x < kernel.GetLength(1); x++)
            {
                magickKernel.SetValue(x, y, kernel[y, x]);
            }
        }

        MagickImage magickImage;
        if (sourceImage is Image<L8> gray)
        {
            var pixelBytes = new byte[gray.Width * gray.Height];
            gray.CopyPixelDataTo(pixelBytes);
            var readSettings = new MagickReadSettings
            {
                Format = MagickFormat.Gray,
                Width = (uint)sourceImage.Width,
                Height = (uint)sourceImage.Height
            };
            magickImage = new MagickImage(pixelBytes, readSettings);
        }
        else if (sourceImage is Image<Rgb24> color)
        {
            var pixelBytes = new byte[color.Width * color.Height * 3];
            color.CopyPixelDataTo(pixelBytes);
            var readSettings = new MagickReadSettings
            {
                Format = MagickFormat.Rgb,
                Width = (uint)sourceImage.Width,
                Height = (uint)sourceImage.Height
            };
            magickImage = new MagickImage(pixelBytes, readSettings);
        }
        else
        {
            throw new NotSupportedException("Unsupported pixel format for Magick.NET conversion.");
        }

        using (magickImage)
        {
            magickImage.Convolve(magickKernel);

            if (typeof(T) == typeof(L8))
            {
                var resultPixels = magickImage.GetPixels().ToByteArray("R");
                
                return (Image<T>)(object)Image.LoadPixelData<L8>(resultPixels, sourceImage.Width, sourceImage.Height);
            }
            else
            {
                var resultPixels = magickImage.GetPixels().ToByteArray("RGB");
                return (Image<T>)(object)Image.LoadPixelData<Rgb24>(resultPixels, sourceImage.Width, sourceImage.Height);
            }
        }
    }
    // Compose Kernels tests

    [Fact]
    public void ComposeWithIdentity_ReturnsOriginalKernel()
    {
        var kernel = new double[,]
        {
            { 1, 2, 3 },
            { 4, 5, 6 },
            { 7, 8, 9 }
        };
        var identityKernel = new double[,] { { 1 } };

        var result = ComposeKernels(kernel, identityKernel);

        AssertKernelsAreSimilar(kernel, result);
    }

    [Fact]
    public void ComposeIsCommutative()
    {
        var kernelA = new double[,]
        {
            { 1, 2 },
            { 3, 4 }
        };
        var kernelB = new double[,]
        {
            { 0, 5, 0 },
            { 0, 6, 0 }
        };

        var resultAB = ComposeKernels(kernelA, kernelB);
        var resultBA = ComposeKernels(kernelB, kernelA);

        AssertKernelsAreSimilar(resultAB, resultBA);
    }

    [Fact]
    public void ComposeWithSimpleKernels_ReturnsCorrectHandCalculatedResult()
    {
        var kernelA = new double[,] { { 1, 2 } };
        var kernelB = new double[,] { { 3, 4 } };

        var expected = new double[,] { { 3, 10, 8 } };

        var actual = ComposeKernels(kernelA, kernelB);

        AssertKernelsAreSimilar(expected, actual);
    }

    [Fact]
    public void ComposeWithShiftKernel_ShiftsOriginalKernel()
    {
        var kernel = new double[,]
        {
            { 1, 2 },
            { 3, 4 }
        };

        var shiftKernel = new double[,]
        {
            { 0, 0, 0 },
            { 0, 1, 0 },
            { 0, 0, 0 }
        };

        var expected = new double[,]
        {
            { 0, 0, 0, 0 },
            { 0, 1, 2, 0 },
            { 0, 3, 4, 0 },
            { 0, 0, 0, 0 }
        };

        var actual = ComposeKernels(kernel, shiftKernel);

        AssertKernelsAreSimilar(expected, actual);
    }

    public static IEnumerable<ConvolutionTestCase> GetBaseConvolutionTestCases()
    {
        var dimensions = new[]
        {
            new { Width = 10, Height = 10 },
            new { Width = 500, Height = 100 },
            new { Width = 10, Height = 1000},
            new { Width = 1000, Height = 1000}
        };

        var scenarios = new[]
        {
            new {
                Name = "Grayscale",
                Generator = (Func<int,int,IDisposable>)((w,h) => ImageGenerator.CreateGrayscaleImage(w,h)),
                ApplyFunc = (Func<object,double[,],object>)((img,k) => ConvolutionAlgorithm.Apply((Image<L8>)img, k)),
                AssertEqualFunc = (Action<object,object>)((img1,img2) => AssertImagesAreEqual((Image<L8>)img1, (Image<L8>)img2)),
                AssertSimilarFunc = (Action<object,object>)((img1,img2) => AssertImagesAreSimilar((Image<L8>)img1, (Image<L8>)img2))
            },
            new {
                Name = "Color",
                Generator = (Func<int,int,IDisposable>)((w,h) => ImageGenerator.CreateColorImage(w,h)),
                ApplyFunc = (Func<object,double[,],object>)((img,k) => ConvolutionAlgorithm.Apply((Image<Rgb24>)img, k)),
                AssertEqualFunc = (Action<object,object>)((img1,img2) => AssertImagesAreEqual((Image<Rgb24>)img1, (Image<Rgb24>)img2)),
                AssertSimilarFunc = (Action<object,object>)((img1,img2) => AssertImagesAreSimilar((Image<Rgb24>)img1, (Image<Rgb24>)img2))
            },
            new {
                Name = "Color Optimized",
                Generator = (Func<int,int,IDisposable>)((w,h) => ImageGenerator.CreateColorImage(w,h)),
                ApplyFunc = (Func<object,double[,],object>)((img,k) => ConvolutionAlgorithm.ApplyOptimized((Image<Rgb24>)img, k)),
                AssertEqualFunc = (Action<object,object>)((img1,img2) => AssertImagesAreEqual((Image<Rgb24>)img1, (Image<Rgb24>)img2)),
                AssertSimilarFunc = (Action<object,object>)((img1,img2) => AssertImagesAreSimilar((Image<Rgb24>)img1, (Image<Rgb24>)img2))
            }
        };

        foreach (var dim in dimensions)
        {
            foreach (var scenario in scenarios)
            {
                yield return new ConvolutionTestCase(dim.Width, dim.Height, scenario.Name, scenario.Generator, scenario.ApplyFunc, scenario.AssertEqualFunc, scenario.AssertSimilarFunc);
            }
        }
    }

    public static TheoryData<ConvolutionTestCase> GetConvolutionTestCases()
    {
        var data = new TheoryData<ConvolutionTestCase>();
        foreach (var testCase in GetBaseConvolutionTestCases())
        {
            data.Add(testCase);
        }
        return data;
    }

    public class ConvolutionTestCase
    {
        private readonly string _displayName;
        public int Width { get; }
        public int Height { get; }
        public Func<int, int, IDisposable> ImageGenerator { get; }
        public Func<object, double[,], object> ApplyConvolution { get; }
        public Action<object, object> AssertEquality { get; }
        public Action<object, object> AssertSimilarity { get; }

        public ConvolutionTestCase(
            int width,
            int height,
            string scenarioName,
            Func<int, int, IDisposable> imageGenerator,
            Func<object, double[,], object> applyConvolution,
            Action<object, object> assertEquality,
            Action<object, object> assertSimilarity)
        {
            Width = width;
            Height = height;
            ImageGenerator = imageGenerator;
            ApplyConvolution = applyConvolution;
            AssertEquality = assertEquality;
            AssertSimilarity = assertSimilarity;
            _displayName = $"{scenarioName} ({width}x{height})";
        }

        public override string ToString() => _displayName;
    }

    public static TheoryData<ConvolutionTestCase, string, double[,]> GetValidationTestCases()
    {
        var data = new TheoryData<ConvolutionTestCase, string, double[,]>();

        var baseCases = GetBaseConvolutionTestCases();

        var kernelsToValidate = new Dictionary<string, double[,]>
        {
            { "Identity", Kernels.Identity },
            { "BoxBlur", Kernels.BoxBlur },
            { "Sharpen", Kernels.Sharpen },
            { "EdgeDetection", Kernels.EdgeDetection },
            { "Zero", Kernels.Zero },
            { "Laplacian3x3", Kernels.Laplacian3x3 },
            { "Laplacian5x5", Kernels.Laplacian5x5 },
            { "SobelX", Kernels.SobelX },
            { "SobelY", Kernels.SobelY },
        };

        foreach (var testCase in baseCases)
        {
            foreach (var kernelEntry in kernelsToValidate)
            {
                data.Add(testCase, kernelEntry.Key, kernelEntry.Value);
            }
        }
        
        return data;
    }

    // Parametrized module tests

    [Theory]
    [MemberData(nameof(GetConvolutionTestCases))]
    public void ApplyIdentityKernel_ReturnsIdenticalImage(ConvolutionTestCase testCase)
    {
        using var sourceImage = testCase.ImageGenerator(testCase.Width, testCase.Height);
        
        object expectedImage;
        if (sourceImage is Image<L8> gray) expectedImage = gray.Clone();
        else if (sourceImage is Image<Rgb24> color) expectedImage = color.Clone();
        else throw new NotSupportedException("Unsupported image type for cloning.");

        using var resultImage = (IDisposable)testCase.ApplyConvolution(sourceImage, Kernels.Identity);
        
        testCase.AssertEquality(expectedImage, resultImage);

        ((IDisposable)expectedImage).Dispose();
    }


    [Theory]
    [MemberData(nameof(GetConvolutionTestCases))]
    public void ApplyZeroKernel_ReturnsBlackImage(ConvolutionTestCase testCase)
    {
        using var sourceImage = testCase.ImageGenerator(testCase.Width, testCase.Height);
        using var resultImage = (IDisposable)testCase.ApplyConvolution(sourceImage, Kernels.Zero);

        if (resultImage is Image<L8> grayResult)
        {
            for (int y = 0; y < grayResult.Height; y++)
            for (int x = 0; x < grayResult.Width; x++)
                Assert.Equal(0, grayResult[x, y].PackedValue);
        }
        else if (resultImage is Image<Rgb24> colorResult)
        {
            var blackPixel = new Rgb24(0, 0, 0);
            for (int y = 0; y < colorResult.Height; y++)
            for (int x = 0; x < colorResult.Width; x++)
                Assert.Equal(blackPixel, colorResult[x, y]);
        }
    }

    [Theory]
    [MemberData(nameof(GetValidationTestCases))]
    public void ApplyPaddedKernel_ReturnsSameResultAsOriginal(
        ConvolutionTestCase testCase, 
        string kernelName,
        double[,] originalKernel)
    {
        if (kernelName == "Identity" || kernelName == "Zero")
        {
            return;
        }

        var paddingKernel = new double[,]
        {
            { 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0 },
            { 0, 0, 1, 0, 0 },
            { 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0 }
        };

        var paddedKernel = ComposeKernels(originalKernel, paddingKernel);

        using var sourceImage = testCase.ImageGenerator(testCase.Width, testCase.Height);

        using var resultOriginal = (IDisposable)testCase.ApplyConvolution(sourceImage, originalKernel);
        using var resultPadded = (IDisposable)testCase.ApplyConvolution(sourceImage, paddedKernel);

        testCase.AssertEquality(resultOriginal, resultPadded);
    }

    [Theory]
    [MemberData(nameof(GetConvolutionTestCases))]
    public void ComposeFilters_IsEquivalentToSequentialApplication(ConvolutionTestCase testCase)
    {
        var kernelA = Kernels.BoxBlur;
        var kernelB = new double[,]
        {
            {1.0/16, 2.0/16, 1.0/16},
            {2.0/16, 4.0/16, 2.0/16},
            {1.0/16, 2.0/16, 1.0/16}
        };
        var composedKernel = ComposeKernels(kernelA, kernelB);

        using var sourceImage = testCase.ImageGenerator(testCase.Width, testCase.Height);
        
        using var intermediateImage = (IDisposable)testCase.ApplyConvolution(sourceImage, kernelA);
        using var sequentialResult = (IDisposable)testCase.ApplyConvolution(intermediateImage, kernelB);
        using var compositeResult = (IDisposable)testCase.ApplyConvolution(sourceImage, composedKernel);
        
        testCase.AssertSimilarity(sequentialResult, compositeResult);
    }
    
    [Theory]
    [MemberData(nameof(GetConvolutionTestCases))]
    public void BoxBlur_Matches_ImageSharpImplementation(ConvolutionTestCase testCase)
    {
        using var sourceImage = (Image)testCase.ImageGenerator(testCase.Width, testCase.Height);

        using var myResultImage = (IDisposable)testCase.ApplyConvolution(sourceImage, Kernels.BoxBlur);

        using var imageSharpResult = sourceImage.Clone(ctx => ctx.BoxBlur(1));

        testCase.AssertSimilarity(myResultImage, imageSharpResult);
    }


    [Theory]
    [MemberData(nameof(GetValidationTestCases))]
    public void CompareToMagickNet_OnArbitraryKernel(ConvolutionTestCase testCase, string _, double[,] kernel)
    {
        using var sourceImage = (Image)testCase.ImageGenerator(testCase.Width, testCase.Height);

        using var myResult = (IDisposable)testCase.ApplyConvolution(sourceImage, kernel);
        
        IDisposable magickNetResult;
        if (sourceImage is Image<L8> gray)
        {
            magickNetResult = GetMagickNetResult(gray, kernel);
        }
        else
        {
            magickNetResult = GetMagickNetResult((Image<Rgb24>)sourceImage, kernel);
        }
        
        testCase.AssertEquality(myResult, magickNetResult);

        magickNetResult.Dispose();
    }
}