using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SkiaSharp;

namespace Jellyfin.Plugin.ELearning.Library;

/// <summary>
/// Draws generated posters for courses and sections: a blurred purple/blue gradient in Jellyfin's
/// logo colours with the title on top. The blob layout is seeded by the title, so covers differ a
/// little from course to course but a course always gets the same one.
/// </summary>
internal static class CoverRenderer
{
    /// <summary>Poster width; Jellyfin's portrait posters are 2:3.</summary>
    public const int Width = 800;

    /// <summary>Poster height.</summary>
    public const int Height = 1200;

    /// <summary>Bump when the design changes, so every cover is redrawn on the next scan.</summary>
    public const int DesignVersion = 1;

    private const float Margin = 72;
    private const int MaxTitleLines = 6;

    private static readonly SKColor _purple = new(0xAA, 0x5C, 0xC3);
    private static readonly SKColor _blue = new(0x00, 0xA4, 0xDC);
    private static readonly SKColor _violet = new(0x6B, 0x4F, 0xE0);
    private static readonly SKColor _night = new(0x14, 0x12, 0x2E);

    /// <summary>
    /// Renders a cover as JPEG.
    /// </summary>
    /// <param name="title">The main text (course or section title).</param>
    /// <param name="label">Small text above the title ("SECTION 3"), if any.</param>
    /// <param name="footer">Small text at the bottom (studio or course title), if any.</param>
    /// <returns>The JPEG bytes.</returns>
    public static byte[] Render(string title, string? label, string? footer)
    {
        ArgumentNullException.ThrowIfNull(title);
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        DrawBackground(canvas, new Random(Seed(title)));

        using var bold = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Bold) ?? SKTypeface.Default;
        using var regular = SKTypeface.FromFamilyName("DejaVu Sans", SKFontStyle.Normal) ?? SKTypeface.Default;
        using var shadow = SKImageFilter.CreateDropShadow(0, 4, 10, 10, new SKColor(0, 0, 0, 110));
        using var paint = new SKPaint { IsAntialias = true, Color = SKColors.White, ImageFilter = shadow };

        using var titleFont = new SKFont(bold) { Subpixel = true };
        var lines = FitTitle(title, titleFont);
        var lineHeight = titleFont.Size * 1.18f;
        using var labelFont = new SKFont(bold, 30) { Subpixel = true };
        var labelHeight = label is null ? 0 : labelFont.Size * 2f;

        // The title block sits slightly above the middle; the footer has the bottom to itself.
        var blockHeight = labelHeight + (lines.Count * lineHeight);
        var top = (Height * 0.46f) - (blockHeight / 2);
        if (label is not null)
        {
            paint.Color = SKColors.White.WithAlpha(200);
            canvas.DrawText(label.ToUpperInvariant(), Width / 2f, top + labelFont.Size, SKTextAlign.Center, labelFont, paint);
        }

        paint.Color = SKColors.White;
        var baseline = top + labelHeight + titleFont.Size;
        foreach (var line in lines)
        {
            canvas.DrawText(line, Width / 2f, baseline, SKTextAlign.Center, titleFont, paint);
            baseline += lineHeight;
        }

        if (!string.IsNullOrWhiteSpace(footer))
        {
            using var footerFont = new SKFont(regular, 32) { Subpixel = true };
            var footerLines = Ellipsize(Wrap(footer, footerFont, Width - (2 * Margin)), 2, footerFont, Width - (2 * Margin));
            paint.Color = SKColors.White.WithAlpha(215);
            var y = Height - Margin - ((footerLines.Count - 1) * footerFont.Size * 1.25f);
            foreach (var line in footerLines)
            {
                canvas.DrawText(line, Width / 2f, y, SKTextAlign.Center, footerFont, paint);
                y += footerFont.Size * 1.25f;
            }
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    /// <summary>
    /// Breaks text into lines no wider than <paramref name="maxWidth"/>; a single word that is
    /// wider stays on a line of its own.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The font.</param>
    /// <param name="maxWidth">The maximum line width.</param>
    /// <returns>The lines.</returns>
    internal static List<string> Wrap(string text, SKFont font, float maxWidth)
    {
        var lines = new List<string>();
        var line = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && font.MeasureText(candidate) > maxWidth)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = candidate;
            }
        }

        if (line.Length > 0)
        {
            lines.Add(line);
        }

        return lines;
    }

    // Largest size at which the title fits; past the smallest size, cut it off with an ellipsis.
    private static List<string> FitTitle(string title, SKFont font)
    {
        const float MaxWidth = Width - (2 * Margin);
        List<string> lines = [];
        for (var size = 96f; size >= 44f; size -= 4f)
        {
            font.Size = size;
            lines = Wrap(title, font, MaxWidth);
            if (lines.Count <= MaxTitleLines && lines.All(l => font.MeasureText(l) <= MaxWidth))
            {
                return lines;
            }
        }

        return Ellipsize(lines, MaxTitleLines, font, MaxWidth);
    }

    private static List<string> Ellipsize(List<string> lines, int maxLines, SKFont font, float maxWidth)
    {
        if (lines.Count <= maxLines && lines.All(l => font.MeasureText(l) <= maxWidth))
        {
            return lines;
        }

        var kept = lines.Take(maxLines).ToList();
        var last = kept[^1];
        while (last.Length > 1 && font.MeasureText(last + "…") > maxWidth)
        {
            last = last[..^1].TrimEnd();
        }

        kept[^1] = last + "…";
        return kept;
    }

    private static void DrawBackground(SKCanvas canvas, Random random)
    {
        canvas.Clear(_night);

        // Base: diagonal purple → blue, at a slightly different angle per course.
        var tilt = (float)(random.NextDouble() * 0.4);
        using (var gradient = SKShader.CreateLinearGradient(
            new SKPoint(0, Height * tilt),
            new SKPoint(Width, Height * (1 - tilt)),
            [_purple, _violet, _blue],
            [0f, 0.5f, 1f],
            SKShaderTileMode.Clamp))
        using (var paint = new SKPaint { Shader = gradient })
        {
            canvas.DrawRect(0, 0, Width, Height, paint);
        }

        // Soft blobs, blurred into each other like the logo's glow.
        using (var blur = SKImageFilter.CreateBlur(140, 140))
        using (var paint = new SKPaint { IsAntialias = true, ImageFilter = blur })
        {
            foreach (var color in new[] { _purple, _blue, _violet, _blue.WithAlpha(200) })
            {
                paint.Color = color;
                canvas.DrawCircle(
                    (float)(random.NextDouble() * Width),
                    (float)(random.NextDouble() * Height),
                    (float)((0.3 + (random.NextDouble() * 0.3)) * Width),
                    paint);
            }
        }

        // Darken towards the edges so the white text stays readable.
        using (var vignette = SKShader.CreateRadialGradient(
            new SKPoint(Width / 2f, Height * 0.46f),
            Height * 0.75f,
            [SKColors.Transparent, _night.WithAlpha(170)],
            [0.35f, 1f],
            SKShaderTileMode.Clamp))
        using (var paint = new SKPaint { Shader = vignette })
        {
            canvas.DrawRect(0, 0, Width, Height, paint);
        }
    }

    // string.GetHashCode is randomized per process; covers must come out the same every time.
    private static int Seed(string title)
        => BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(title)), 0);
}
