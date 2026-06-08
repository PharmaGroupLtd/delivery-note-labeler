using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using DeliveryNoteLabeler.Core.Printing;

namespace DeliveryNoteLabeler.Services;

internal static class ZplBitmapEncoder
{
    private const int SuperSampleFactor = 3;
    private const byte DarkInkThreshold = 220;

    public static ZplEmbeddedGraphic? CreateLogoGraphic(string imagePath, LabelLayoutOptions layout, int contentTopDots)
    {
        if (!File.Exists(imagePath))
        {
            return null;
        }

        using var source = new Bitmap(imagePath);
        var crop = DetectCropBounds(source);
        if (crop.Width <= 0 || crop.Height <= 0)
        {
            return null;
        }

        var (maxWidth, maxHeight) = LabelLayoutMetrics.GetLogoBounds(layout, contentTopDots);
        var scale = Math.Min(maxWidth / (double)crop.Width, maxHeight / (double)crop.Height);
        var width = Math.Max(1, (int)Math.Round(crop.Width * scale));
        var height = Math.Max(1, (int)Math.Round(crop.Height * scale));

        while (GetGraphicByteCount(width, height) > LabelLayoutMetrics.MaxLogoGraphicBytes &&
               scale > 0.05)
        {
            scale *= 0.9;
            width = Math.Max(1, (int)Math.Round(crop.Width * scale));
            height = Math.Max(1, (int)Math.Round(crop.Height * scale));
        }

        if (height > maxHeight)
        {
            var heightScale = maxHeight / (double)height;
            width = Math.Max(1, (int)Math.Round(width * heightScale));
            height = Math.Max(1, (int)Math.Round(height * heightScale));
        }

        var superWidth = width * SuperSampleFactor;
        var superHeight = height * SuperSampleFactor;

        using var superSampled = new Bitmap(superWidth, superHeight, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(superSampled))
        {
            graphics.Clear(Color.White);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(
                source,
                new Rectangle(0, 0, superWidth, superHeight),
                crop,
                GraphicsUnit.Pixel);
        }

        var data = ExtractSolidBlackGraphic(superSampled, width, height);
        var (originX, originY) = LabelLayoutMetrics.GetLogoOrigin(layout, width, height, contentTopDots);
        var bytesPerRow = (width + 7) / 8;
        var totalBytes = data.Length;
        var hex = Convert.ToHexString(data);
        if (data.Length != GetGraphicByteCount(width, height))
        {
            throw new InvalidOperationException("Logo graphic byte count mismatch.");
        }

        return new ZplEmbeddedGraphic
        {
            WidthDots = width,
            HeightDots = height,
            OriginX = originX,
            OriginY = originY,
            GraphicFieldCommand = new StringBuilder()
                .Append("^GFA,")
                .Append(totalBytes)
                .Append(',')
                .Append(totalBytes)
                .Append(',')
                .Append(bytesPerRow)
                .Append(',')
                .Append(hex)
                .ToString(),
        };
    }

    private static Rectangle DetectCropBounds(Bitmap source)
    {
        var width = source.Width;
        var height = source.Height;
        var minX = width;
        var minY = height;
        var maxX = 0;
        var maxY = 0;

        var bitmapData = source.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            var stride = bitmapData.Stride;
            var buffer = new byte[stride * height];
            Marshal.Copy(bitmapData.Scan0, buffer, 0, buffer.Length);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (!IsInkPixel(buffer, stride, x, y))
                    {
                        continue;
                    }

                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }
        }
        finally
        {
            source.UnlockBits(bitmapData);
        }

        if (maxX < minX || maxY < minY)
        {
            return Rectangle.Empty;
        }

        return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
    }

    private static byte[] ExtractSolidBlackGraphic(Bitmap superSampled, int width, int height)
    {
        var superWidth = superSampled.Width;
        var superHeight = superSampled.Height;
        var bytesPerRow = (width + 7) / 8;
        var output = new byte[bytesPerRow * height];

        var bitmapData = superSampled.LockBits(
            new Rectangle(0, 0, superWidth, superHeight),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            var stride = bitmapData.Stride;
            var buffer = new byte[stride * superHeight];
            Marshal.Copy(bitmapData.Scan0, buffer, 0, buffer.Length);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var inkCount = 0;

                    var startX = x * SuperSampleFactor;
                    var startY = y * SuperSampleFactor;

                    for (var sy = startY; sy < startY + SuperSampleFactor && sy < superHeight; sy++)
                    {
                        for (var sx = startX; sx < startX + SuperSampleFactor && sx < superWidth; sx++)
                        {
                            if (IsInkPixel(buffer, stride, sx, sy))
                            {
                                inkCount++;
                            }
                        }
                    }

                    if (inkCount == 0)
                    {
                        continue;
                    }

                    var byteIndex = (y * bytesPerRow) + (x / 8);
                    output[byteIndex] |= (byte)(0x80 >> (x % 8));
                }
            }
        }
        finally
        {
            superSampled.UnlockBits(bitmapData);
        }

        return output;
    }

    private static bool IsInkPixel(byte[] buffer, int stride, int x, int y)
    {
        var offset = (y * stride) + (x * 4);
        var alpha = buffer[offset + 3];
        if (alpha < 16)
        {
            return false;
        }

        var luminance = GetLuminance(buffer, offset);
        return luminance < DarkInkThreshold;
    }

    private static int GetGraphicByteCount(int widthDots, int heightDots) =>
        ((widthDots + 7) / 8) * heightDots;

    private static double GetLuminance(byte[] buffer, int offset)
    {
        var blue = buffer[offset];
        var green = buffer[offset + 1];
        var red = buffer[offset + 2];
        return (red * 0.299) + (green * 0.587) + (blue * 0.114);
    }
}
