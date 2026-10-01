namespace Nomi;

public sealed class ScreenFrame
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    public ScreenFrame(int width, int height, byte[] pixels)
    {
        if (width <= 0 || height <= 0 || pixels.Length != width * height * 4)
            throw new ArgumentException("Invalid frame.", nameof(pixels));
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public ScreenFrame Fit(int maxSide, int minPixels = 0)
    {
        var limit = (double)maxSide / Math.Max(Width, Height);
        var scale = Math.Min(1.0, limit);
        var area = (double)Width * Height;
        if (area * scale * scale < minPixels) scale = Math.Min(limit, Math.Sqrt(minPixels / area));
        if (Math.Abs(scale - 1) < 0.001) return this;
        var width = Math.Max(1, (int)Math.Round(Width * scale));
        var height = Math.Max(1, (int)Math.Round(Height * scale));
        var output = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var top = y * Height / height;
            var bottom = Math.Max(top + 1, (y + 1) * Height / height);
            for (var x = 0; x < width; x++)
            {
                var left = x * Width / width;
                var right = Math.Max(left + 1, (x + 1) * Width / width);
                long b = 0, g = 0, r = 0, count = 0;
                for (var sy = top; sy < bottom; sy++)
                {
                    var row = sy * Width * 4;
                    for (var sx = left; sx < right; sx++)
                    {
                        var index = row + sx * 4;
                        b += Pixels[index];
                        g += Pixels[index + 1];
                        r += Pixels[index + 2];
                        count++;
                    }
                }
                var target = (y * width + x) * 4;
                output[target] = (byte)(b / count);
                output[target + 1] = (byte)(g / count);
                output[target + 2] = (byte)(r / count);
                output[target + 3] = 255;
            }
        }
        return new ScreenFrame(width, height, output);
    }

    public bool IsBlank()
    {
        var first = Pixels.AsSpan(0, 3).ToArray();
        var step = Math.Max(1, Width * Height / 4096) * 4;
        for (var index = 0; index < Pixels.Length; index += step)
        {
            if (Math.Abs(Pixels[index] - first[0]) > 6 || Math.Abs(Pixels[index + 1] - first[1]) > 6
                || Math.Abs(Pixels[index + 2] - first[2]) > 6) return false;
        }
        return true;
    }

    public byte[] Thumbprint()
    {
        var small = Fit(32);
        var print = new byte[small.Width * small.Height];
        for (var index = 0; index < print.Length; index++)
            print[index] = (byte)((small.Pixels[index * 4] + small.Pixels[index * 4 + 1] * 2 + small.Pixels[index * 4 + 2]) / 4);
        return print;
    }

    public static double Difference(byte[]? first, byte[]? second)
    {
        if (first is null || second is null || first.Length != second.Length || first.Length == 0) return 1;
        long total = 0;
        for (var index = 0; index < first.Length; index++) total += Math.Abs(first[index] - second[index]);
        return total / (255.0 * first.Length);
    }

    public byte[] ToBitmap()
    {
        var stride = (Width * 3 + 3) & ~3;
        var size = 54 + stride * Height;
        var file = new byte[size];
        file[0] = (byte)'B';
        file[1] = (byte)'M';
        BitConverter.GetBytes(size).CopyTo(file, 2);
        BitConverter.GetBytes(54).CopyTo(file, 10);
        BitConverter.GetBytes(40).CopyTo(file, 14);
        BitConverter.GetBytes(Width).CopyTo(file, 18);
        BitConverter.GetBytes(Height).CopyTo(file, 22);
        BitConverter.GetBytes((short)1).CopyTo(file, 26);
        BitConverter.GetBytes((short)24).CopyTo(file, 28);
        BitConverter.GetBytes(stride * Height).CopyTo(file, 34);
        for (var y = 0; y < Height; y++)
        {
            var source = y * Width * 4;
            var target = 54 + (Height - 1 - y) * stride;
            for (var x = 0; x < Width; x++)
            {
                file[target + x * 3] = Pixels[source + x * 4];
                file[target + x * 3 + 1] = Pixels[source + x * 4 + 1];
                file[target + x * 3 + 2] = Pixels[source + x * 4 + 2];
            }
        }
        return file;
    }
}
