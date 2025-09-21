using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Concurrent;

namespace Convolution.Main;

public static class ParallelConvolutionAlgorithm
{
    private static void ProcessRowOptimized(Image<Rgb24> sourceImage, Image<Rgb24> resultImage, double[,] kernel, int y)
    {
        int width = sourceImage.Width;
        int height = sourceImage.Height;
        int kernelSize = kernel.GetLength(0);
        int kernelOffset = kernelSize / 2;
        
        var rowBuffer = new Rgb24[kernelSize * width];

        for (int i = 0; i < kernelSize; i++)
        {
            int sourceY = Math.Clamp(y + i - kernelOffset, 0, height - 1);
            sourceImage.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(sourceY)
                .CopyTo(new Span<Rgb24>(rowBuffer, i * width, width));
        }

        for (int x = 0; x < width; x++)
        {
            double sumR = 0, sumG = 0, sumB = 0;
            for (int ky = 0; ky < kernelSize; ky++)
            {
                for (int kx = 0; kx < kernelSize; kx++)
                {
                    int bufferX = Math.Clamp(x - kernelOffset + kx, 0, width - 1);
                    Rgb24 pixel = rowBuffer[ky * width + bufferX];
                    double kernelValue = kernel[kernelSize - 1 - ky, kernelSize - 1 - kx];

                    sumR += pixel.R * kernelValue;
                    sumG += pixel.G * kernelValue;
                    sumB += pixel.B * kernelValue;
                }
            }

            resultImage[x, y] = new Rgb24(
                (byte)Math.Clamp(Math.Round(sumR), 0, 255),
                (byte)Math.Clamp(Math.Round(sumG), 0, 255),
                (byte)Math.Clamp(Math.Round(sumB), 0, 255)
            );
        }
    }

    public static Image<Rgb24> ApplyParallelByRow(Image<Rgb24> sourceImage, double[,] kernel)
    {
        int width = sourceImage.Width;
        int height = sourceImage.Height;
        var resultImage = new Image<Rgb24>(width, height);

        Parallel.For(0, height, y =>
        {
            ProcessRowOptimized(sourceImage, resultImage, kernel, y);
        });

        return resultImage;
    }

    public static Image<Rgb24> ApplyParallelByColumn(Image<Rgb24> sourceImage, double[,] kernel)
    {
        int width = sourceImage.Width;
        int height = sourceImage.Height;
        var resultImage = new Image<Rgb24>(width, height);
        
        int kernelWidth = kernel.GetLength(1);
        int kernelHeight = kernel.GetLength(0);
        int kernelOffsetX = kernelWidth / 2;
        int kernelOffsetY = kernelHeight / 2;

        Parallel.For(0, width, x =>
        {
            for (int y = 0; y < height; y++)
            {
                double sumR = 0, sumG = 0, sumB = 0;
                for (int ky = 0; ky < kernelHeight; ky++)
                {
                    for (int kx = 0; kx < kernelWidth; kx++)
                    {
                        int sourceX = Math.Clamp(x + kernelOffsetX - kx, 0, width - 1);
                        int sourceY = Math.Clamp(y + kernelOffsetY - ky, 0, height - 1);
                            
                        Rgb24 sourcePixel = sourceImage[sourceX, sourceY];
                        double kernelValue = kernel[ky, kx];

                        sumR += sourcePixel.R * kernelValue;
                        sumG += sourcePixel.G * kernelValue;
                        sumB += sourcePixel.B * kernelValue;
                    }
                }
                resultImage[x, y] = new Rgb24(
                    (byte)Math.Clamp(Math.Round(sumR), 0, 255),
                    (byte)Math.Clamp(Math.Round(sumG), 0, 255),
                    (byte)Math.Clamp(Math.Round(sumB), 0, 255)
                );
            }
        });
        return resultImage;
    }

    public static Image<Rgb24> ApplyParallelByTile(Image<Rgb24> sourceImage, double[,] kernel)
    {
        int width = sourceImage.Width;
        int height = sourceImage.Height;
        var resultImage = new Image<Rgb24>(width, height);
        
        var tiles = Partitioner.Create(0, height);

        Parallel.ForEach(tiles, range =>
        {
            for (int y = range.Item1; y < range.Item2; y++)
            {
                ProcessRowOptimized(sourceImage, resultImage, kernel, y);
            }
        });
        
        return resultImage;
    }

    private static void CalculateSinglePixelValue(Image<Rgb24> sourceImage, Image<Rgb24> resultImage, double[,] kernel, int x, int y)
    {
        int width = sourceImage.Width;
        int height = sourceImage.Height;
        int kernelWidth = kernel.GetLength(1);
        int kernelHeight = kernel.GetLength(0);
        int kernelOffsetX = kernelWidth / 2;
        int kernelOffsetY = kernelHeight / 2;
        
        double sumR = 0, sumG = 0, sumB = 0;
        for (int ky = 0; ky < kernelHeight; ky++)
        {
            for (int kx = 0; kx < kernelWidth; kx++)
            {
                int sourceX = Math.Clamp(x + kernelOffsetX - kx, 0, width - 1);
                int sourceY = Math.Clamp(y + kernelOffsetY - ky, 0, height - 1);
                            
                Rgb24 sourcePixel = sourceImage[sourceX, sourceY];
                double kernelValue = kernel[ky, kx];

                sumR += sourcePixel.R * kernelValue;
                sumG += sourcePixel.G * kernelValue;
                sumB += sourcePixel.B * kernelValue;
            }
        }
        resultImage[x, y] = new Rgb24(
            (byte)Math.Clamp(Math.Round(sumR), 0, 255),
            (byte)Math.Clamp(Math.Round(sumG), 0, 255),
            (byte)Math.Clamp(Math.Round(sumB), 0, 255)
        );
    }
    
    public static Image<Rgb24> ApplyParallelByPixel(Image<Rgb24> sourceImage, double[,] kernel)
    {
        int width = sourceImage.Width;
        int height = sourceImage.Height;
        var resultImage = new Image<Rgb24>(width, height);

        Parallel.For(0, width * height, i =>
        {
            int x = i % width;
            int y = i / width;
            
            CalculateSinglePixelValue(sourceImage, resultImage, kernel, x, y);
        });
        
        return resultImage;
    }
}