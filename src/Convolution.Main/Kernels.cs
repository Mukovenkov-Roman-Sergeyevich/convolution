namespace Convolution.Main;

public static class Kernels
{
    public static readonly double[,] Identity =
    {
        {0, 0, 0},
        {0, 1, 0},
        {0, 0, 0}
    };

    public static readonly double[,] EdgeDetection =
    {
        {-1, -1, -1},
        {-1,  8, -1},
        {-1, -1, -1}
    };

    public static readonly double[,] Zero =
    {
        {0, 0, 0},
        {0, 0, 0},
        {0, 0, 0}
    };

    public static readonly double[,] Sharpen =
    {
        { 0,-1, 0},
        {-1, 5,-1},
        { 0,-1, 0}
    };

    public static readonly double[,] BoxBlur =
    {
        {1.0 / 9.0, 1.0 / 9.0, 1.0 / 9.0},
        {1.0 / 9.0, 1.0 / 9.0, 1.0 / 9.0},
        {1.0 / 9.0, 1.0 / 9.0, 1.0 / 9.0}
    };

        public static readonly double[,] SobelX =
    {
        { -1, 0, 1 },
        { -2, 0, 2 },
        { -1, 0, 1 }
    };

    public static readonly double[,] SobelY =
    {
        { -1, -2, -1 },
        {  0,  0,  0 },
        {  1,  2,  1 }
    };

    public static readonly double[,] Laplacian3x3 =
    {
        {  0, -1,  0 },
        { -1,  4, -1 },
        {  0, -1,  0 }
    };

    public static readonly double[,] Laplacian5x5 =
    {
        { -1, -1, -1, -1, -1 },
        { -1, -1, -1, -1, -1 },
        { -1, -1, 24, -1, -1 },
        { -1, -1, -1, -1, -1 },
        { -1, -1, -1, -1, -1 }
    };

        public static readonly double[,] Random =
    {
   {0,  0, -1,  0,  0},
   {0,  0, -1,  0,  0},
   {0,  0,  4,  0,  0},
   {0,  0, -1,  0,  0},
   {0,  0, -1,  0,  0}
    };

    public static double[,] GetKernelByName(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "identity"      => Identity,
            "edgedetection" => EdgeDetection,
            "sharpen"       => Sharpen,
            "boxblur"       => BoxBlur,
            "zero"          => Zero,
            "sobelx"        => SobelX,
            "sobely"        => SobelY,
            "laplacian3x3"  => Laplacian3x3,
            "laplacian5x5"  => Laplacian5x5,
            "random"        => Random,
            _ => throw new ArgumentException($"Test setup error: Kernel '{name}' is not known.")
        };
    }
}
