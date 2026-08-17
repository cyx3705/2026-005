using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopIconUpgrader.Models;

namespace DesktopIconUpgrader.Services;

public sealed class IconRenderer
{
    public IconAnalysis Analyze(BitmapSource source)
    {
        var sampled = Scale(source, 128, 128, Stretch.Uniform);
        var bgra = ToBgra32(sampled);
        var stride = bgra.PixelWidth * 4;
        var pixels = new byte[stride * bgra.PixelHeight];
        bgra.CopyPixels(pixels, stride, 0);
        var transparent = 0;
        var histogram = new Dictionary<int, int>();
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            var alpha = pixels[offset + 3];
            if (alpha < 24)
            {
                transparent++;
                continue;
            }
            var red = pixels[offset + 2] >> 3;
            var green = pixels[offset + 1] >> 3;
            var blue = pixels[offset] >> 3;
            var key = (red << 10) | (green << 5) | blue;
            histogram[key] = histogram.GetValueOrDefault(key) + 1;
        }

        var ratio = (double)transparent / (bgra.PixelWidth * bgra.PixelHeight);
        var cornerAlpha = new[]
        {
            pixels[3],
            pixels[(bgra.PixelWidth - 1) * 4 + 3],
            pixels[(bgra.PixelHeight - 1) * stride + 3],
            pixels[(bgra.PixelHeight - 1) * stride + (bgra.PixelWidth - 1) * 4 + 3]
        };
        var kind = ratio >= 0.025 || cornerAlpha.Min() < 210 ? BackgroundKind.Transparent : BackgroundKind.Colored;
        var bestColor = (R: (byte)55, G: (byte)116, B: (byte)215);
        var bestScore = double.MinValue;
        foreach (var pair in histogram)
        {
            var r = (byte)((((pair.Key >> 10) & 31) << 3) | 4);
            var g = (byte)((((pair.Key >> 5) & 31) << 3) | 4);
            var b = (byte)(((pair.Key & 31) << 3) | 4);
            var max = Math.Max(r, Math.Max(g, b)) / 255d;
            var min = Math.Min(r, Math.Min(g, b)) / 255d;
            var saturation = max <= 0 ? 0 : (max - min) / max;
            var neutralPenalty = saturation < 0.10 ? 0.07 : 1.0;
            var extremePenalty = max < 0.10 || max > 0.97 ? 0.18 : 1.0;
            var score = pair.Value * (0.18 + Math.Min(1.0, saturation * 2.5)) * (0.45 + max * 0.55) * neutralPenalty * extremePenalty;
            if (score <= bestScore) continue;
            bestScore = score;
            bestColor = (r, g, b);
        }

        if (Math.Max(bestColor.R, Math.Max(bestColor.G, bestColor.B)) - Math.Min(bestColor.R, Math.Min(bestColor.G, bestColor.B)) < 18 &&
            (Math.Max(bestColor.R, Math.Max(bestColor.G, bestColor.B)) > 235 || Math.Max(bestColor.R, Math.Max(bestColor.G, bestColor.B)) < 35))
            bestColor = (55, 116, 215);

        var confidence = kind == BackgroundKind.Transparent
            ? Math.Clamp(ratio * 1.8 + (255 - cornerAlpha.Average(value => (double)value)) / 255d, 0.5, 1.0)
            : Math.Clamp(1.0 - ratio * 5, 0.55, 1.0);
        return new IconAnalysis(kind, bestColor.R, bestColor.G, bestColor.B, ratio, confidence);
    }

    public BitmapSource RenderMaster(BitmapSource source, IconAnalysis analysis, int size = 1024)
    {
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var context = visual.RenderOpen())
        {
            var radius = size * 0.22;
            var tile = new Rect(0, 0, size, size);
            context.PushClip(new RectangleGeometry(tile, radius, radius));
            if (analysis.Kind == BackgroundKind.Colored)
            {
                context.DrawImage(source, CoverRect(source, size));
            }
            else
            {
                var baseColor = Lighten(Color.FromRgb(analysis.Red, analysis.Green, analysis.Blue), 0.38);
                var topColor = Lighten(baseColor, 0.20);
                var brush = new LinearGradientBrush(topColor, baseColor, new Point(0.5, 0), new Point(0.5, 1));
                context.DrawRoundedRectangle(brush, null, tile, radius, radius);
                var cropped = CropVisible(source);
                context.DrawImage(cropped, ContainRect(cropped, size * 0.76, size * 0.76, size));
            }
            context.Pop();
        }
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    public static BitmapSource Scale(BitmapSource source, int width, int height, Stretch stretch = Stretch.Uniform)
    {
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var context = visual.RenderOpen())
        {
            var rect = stretch == Stretch.UniformToFill
                ? CoverRect(source, width, height)
                : ContainRect(source, width, height, width, height);
            context.DrawImage(source, rect);
        }
        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }

    private static BitmapSource CropVisible(BitmapSource source)
    {
        var bgra = ToBgra32(source);
        var stride = bgra.PixelWidth * 4;
        var pixels = new byte[stride * bgra.PixelHeight];
        bgra.CopyPixels(pixels, stride, 0);
        var left = bgra.PixelWidth;
        var top = bgra.PixelHeight;
        var right = -1;
        var bottom = -1;
        for (var y = 0; y < bgra.PixelHeight; y++)
        {
            for (var x = 0; x < bgra.PixelWidth; x++)
            {
                if (pixels[y * stride + x * 4 + 3] < 8) continue;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }
        if (right < left || bottom < top) return source;
        var cropped = new CroppedBitmap(bgra, new Int32Rect(left, top, right - left + 1, bottom - top + 1));
        cropped.Freeze();
        return cropped;
    }

    private static BitmapSource ToBgra32(BitmapSource source)
    {
        if (source.Format == PixelFormats.Bgra32 || source.Format == PixelFormats.Pbgra32) return source;
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }

    private static Rect CoverRect(BitmapSource source, double size) => CoverRect(source, size, size);

    private static Rect CoverRect(BitmapSource source, double width, double height)
    {
        var scale = Math.Max(width / source.PixelWidth, height / source.PixelHeight);
        var actualWidth = source.PixelWidth * scale;
        var actualHeight = source.PixelHeight * scale;
        return new Rect((width - actualWidth) / 2, (height - actualHeight) / 2, actualWidth, actualHeight);
    }

    private static Rect ContainRect(BitmapSource source, double maxWidth, double maxHeight, double canvasSize) =>
        ContainRect(source, maxWidth, maxHeight, canvasSize, canvasSize);

    private static Rect ContainRect(BitmapSource source, double maxWidth, double maxHeight, double canvasWidth, double canvasHeight)
    {
        var scale = Math.Min(maxWidth / source.PixelWidth, maxHeight / source.PixelHeight);
        var actualWidth = source.PixelWidth * scale;
        var actualHeight = source.PixelHeight * scale;
        return new Rect((canvasWidth - actualWidth) / 2, (canvasHeight - actualHeight) / 2, actualWidth, actualHeight);
    }

    private static Color Lighten(Color color, double amount) => Color.FromRgb(
        (byte)Math.Round(color.R + (255 - color.R) * amount),
        (byte)Math.Round(color.G + (255 - color.G) * amount),
        (byte)Math.Round(color.B + (255 - color.B) * amount));
}
