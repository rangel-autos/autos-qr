using System.Text;
using SkiaSharp;

namespace GeneradorQr.Core.Rendering;

/// <summary>
/// Texto para la ficha: fuentes Inter embebidas y respaldo del sistema para caracteres que Inter no tiene (emojis).
/// </summary>
internal static class SheetText
{
    private static readonly Lazy<SKTypeface> RegularFace = new(() => Load("Inter-Regular.ttf"));
    private static readonly Lazy<SKTypeface> BoldFace = new(() => Load("Inter-Bold.ttf"));
    private static readonly Lazy<SKTypeface> BlackFace = new(() => Load("Inter-Black.ttf"));

    public static SKTypeface Regular => RegularFace.Value;
    public static SKTypeface Bold => BoldFace.Value;
    public static SKTypeface Black => BlackFace.Value;

    private static SKTypeface Load(string file)
    {
        using var stream = BrandAssets.OpenResource($"GeneradorQr.Core.Assets.Fonts.{file}");
        return SKTypeface.FromStream(stream) ?? SKTypeface.Default;
    }

    /// <summary>Divide el texto en tramos que la fuente principal puede dibujar y tramos con fuente de respaldo.</summary>
    private static IEnumerable<(string Text, SKTypeface Face)> Runs(string text, SKTypeface primary)
    {
        var current = new StringBuilder();
        SKTypeface? currentFace = null;

        foreach (var rune in text.EnumerateRunes())
        {
            var face = primary.ContainsGlyph(rune.Value)
                ? primary
                : SKFontManager.Default.MatchCharacter(rune.Value) ?? primary;

            if (currentFace is not null && !ReferenceEquals(face, currentFace) && face.FamilyName != currentFace.FamilyName)
            {
                yield return (current.ToString(), currentFace);
                current.Clear();
            }

            currentFace = currentFace is null || face.FamilyName != currentFace.FamilyName ? face : currentFace;
            current.Append(rune.ToString());
        }

        if (current.Length > 0 && currentFace is not null)
        {
            yield return (current.ToString(), currentFace);
        }
    }

    public static float Measure(string text, SKTypeface face, float size)
    {
        var width = 0f;
        foreach (var (run, runFace) in Runs(text, face))
        {
            using var paint = new SKPaint { Typeface = runFace, TextSize = size };
            width += paint.MeasureText(run);
        }

        return width;
    }

    /// <summary>Alto de una línea (ascenso + descenso).</summary>
    public static float LineHeight(SKTypeface face, float size)
    {
        using var font = new SKFont(face, size);
        var m = font.Metrics;
        return -m.Ascent + m.Descent;
    }

    /// <summary>Dibuja el texto con su parte superior en <paramref name="top"/>.</summary>
    public static void Draw(SKCanvas canvas, string text, float x, float top, SKTypeface face, float size, SKColor color)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        using var primaryFont = new SKFont(face, size);
        var baseline = top - primaryFont.Metrics.Ascent;

        foreach (var (run, runFace) in Runs(text, face))
        {
            paint.Typeface = runFace;
            paint.TextSize = size;
            paint.SubpixelText = true;
            canvas.DrawText(run, x, baseline, paint);
            x += paint.MeasureText(run);
        }
    }

    public static void DrawCentered(SKCanvas canvas, string text, float centerX, float top, SKTypeface face, float size, SKColor color) =>
        Draw(canvas, text, centerX - Measure(text, face, size) / 2, top, face, size, color);

    /// <summary>Reduce el tamaño de letra hasta que el texto quepa en el ancho indicado.</summary>
    public static float Fit(string text, SKTypeface face, float size, float maxWidth)
    {
        while (size > 1 && Measure(text, face, size) > maxWidth)
        {
            size *= 0.92f;
        }

        return size;
    }

    /// <summary>Acomoda el texto en líneas por palabras; si no cabe, la última línea termina en "…".</summary>
    public static IReadOnlyList<string> Wrap(string text, SKTypeface face, float size, float maxWidth, int maxLines)
    {
        var lines = new List<string>();
        if (maxLines <= 0)
        {
            return lines;
        }

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var line = string.Empty;
        var index = 0;

        while (index < words.Length)
        {
            var candidate = line.Length == 0 ? words[index] : $"{line} {words[index]}";
            if (Measure(candidate, face, size) <= maxWidth)
            {
                line = candidate;
                index++;
                continue;
            }

            if (line.Length == 0)
            {
                // Una sola palabra más ancha que la línea: se corta por caracteres.
                line = CutToWidth(words[index], face, size, maxWidth);
                words[index] = words[index][line.Length..];
                if (words[index].Length == 0) index++;
            }

            lines.Add(line);
            line = string.Empty;
            if (lines.Count == maxLines)
            {
                break;
            }
        }

        if (line.Length > 0 && lines.Count < maxLines)
        {
            lines.Add(line);
        }

        if (index < words.Length && lines.Count > 0)
        {
            lines[^1] = AddEllipsis(lines[^1], face, size, maxWidth);
        }

        return lines;
    }

    private static string CutToWidth(string word, SKTypeface face, float size, float maxWidth)
    {
        var length = word.Length;
        while (length > 1 && Measure(word[..length], face, size) > maxWidth)
        {
            length--;
        }

        if (length < word.Length && char.IsHighSurrogate(word[length - 1]))
        {
            length = Math.Max(1, length - 1);
        }

        return word[..length];
    }

    private static string AddEllipsis(string line, SKTypeface face, float size, float maxWidth)
    {
        var text = line;
        while (text.Length > 0 && Measure(text + "…", face, size) > maxWidth)
        {
            text = text[..^1];
            if (text.Length > 0 && char.IsHighSurrogate(text[^1]))
            {
                text = text[..^1];
            }
        }

        return text.TrimEnd() + "…";
    }
}
