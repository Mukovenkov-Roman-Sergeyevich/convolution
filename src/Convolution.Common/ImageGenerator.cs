using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Convolution.Common
{
    public static class ImageGenerator
    {
        public static Image<L8> CreateGrayscaleImage(int width, int height)
        {
            var image = new Image<L8>(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    image[x, y] = new L8((byte)((y * width + x) % 256));
                }
            }
            return image;
        }

        public static Image<Rgb24> CreateColorImage(int width, int height)
        {
            var image = new Image<Rgb24>(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    image[x, y] = new Rgb24(
                        (byte)(x % 256),
                        (byte)(y % 256),
                        (byte)((x + y) % 256)
                    );
                }
            }
            return image;
        }
    }
}