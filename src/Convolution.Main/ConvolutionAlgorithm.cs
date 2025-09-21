using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace Convolution.Main;

public static class ConvolutionAlgorithm
{
    public static Image<L8> Apply(Image<L8> sourceImage, double[,] kernel)
    {
        int width  = sourceImage.Width;
        int height = sourceImage.Height;
        var resultImage = new Image<L8>(width, height);

        int kernelWidth  = kernel.GetLength(1);
        int kernelHeight = kernel.GetLength(0);
        int kernelOffsetX = kernelWidth  / 2;
        int kernelOffsetY = kernelHeight / 2;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double sum = 0;
                for (int ky = 0; ky < kernelHeight; ky++)
                {
                    for (int kx = 0; kx < kernelWidth; kx++)
                    {
                        int sourceX = Math.Clamp(x + kernelOffsetX - kx, 0, width  - 1);
                        int sourceY = Math.Clamp(y + kernelOffsetY - ky, 0, height - 1);

                        double kernelValue = kernel[ky, kx];
                        sum += sourceImage[sourceX, sourceY].PackedValue * kernelValue;
                    }
                }
                byte resultPixel = (byte)Math.Clamp(Math.Round(sum), 0, 255);
                resultImage[x, y] = new L8(resultPixel);
            }
        }

        return resultImage;
    }

    public static Image<Rgb24> Apply(Image<Rgb24> sourceImage, double[,] kernel)
    {
        int width = sourceImage.Width;
        int height = sourceImage.Height;
        var resultImage = new Image<Rgb24>(width, height);

        int kernelWidth  = kernel.GetLength(1);
        int kernelHeight = kernel.GetLength(0);
        int kernelOffsetX = kernelWidth  / 2;
        int kernelOffsetY = kernelHeight / 2;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double sumR = 0, sumG = 0, sumB = 0;
                for (int ky = 0; ky < kernelHeight; ky++)
                {
                    for (int kx = 0; kx < kernelWidth; kx++)
                    {
                        int sourceX = Math.Clamp(x + kernelOffsetX - kx, 0, width  - 1);
                        int sourceY = Math.Clamp(y + kernelOffsetY - ky, 0, height - 1);

                        Rgb24 sourcePixel = sourceImage[sourceX, sourceY];
                        double kernelValue = kernel[ky, kx];

                        sumR += sourcePixel.R * kernelValue;
                        sumG += sourcePixel.G * kernelValue;
                        sumB += sourcePixel.B * kernelValue;
                    }
                }

                byte resultR = (byte)Math.Clamp(Math.Round(sumR), 0, 255);
                byte resultG = (byte)Math.Clamp(Math.Round(sumG), 0, 255);
                byte resultB = (byte)Math.Clamp(Math.Round(sumB), 0, 255);

                resultImage[x, y] = new Rgb24(resultR, resultG, resultB);
            }
        }

        return resultImage;
    }

    public static Image<Rgb24> ApplyOptimized(Image<Rgb24> sourceImage, double[,] kernel)
    {
        int width = sourceImage.Width;
        int height = sourceImage.Height;
        var resultImage = new Image<Rgb24>(width, height);

        int kernelSize = kernel.GetLength(0);
        int kernelOffset = kernelSize / 2;

        var rowBuffer = new Rgb24[kernelSize * width];

        for (int i = 0; i < kernelSize; i++)
        {
            int sourceY = Math.Clamp(i - kernelOffset, 0, height - 1);

            Span<Rgb24> sourceRowSpan = sourceImage.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(sourceY);
            Span<Rgb24> bufferRowSpan = new Span<Rgb24>(rowBuffer, i * width, width);
            sourceRowSpan.CopyTo(bufferRowSpan);
        }

        for (int y = 0; y < height; y++)
        {
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

            var sourceBufferSpan = new Span<Rgb24>(rowBuffer, width, (kernelSize - 1) * width);
            var destBufferSpan = new Span<Rgb24>(rowBuffer, 0, (kernelSize - 1) * width);
            sourceBufferSpan.CopyTo(destBufferSpan);
            
            int nextRowToLoad = y + kernelOffset + 1;
            if (nextRowToLoad < height)
            {
                Span<Rgb24> sourceRowSpan = sourceImage.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(nextRowToLoad);
                Span<Rgb24> bufferRowSpan = new Span<Rgb24>(rowBuffer, (kernelSize - 1) * width, width);
                sourceRowSpan.CopyTo(bufferRowSpan);
            }
        }

        return resultImage;
    }
}