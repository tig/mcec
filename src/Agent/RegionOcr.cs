// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace MCEControl;

/// <summary>
/// Windows built-in OCR (<see cref="OcrEngine"/>) for agent region text reads (#331). Converts a
/// <see cref="Bitmap"/> to <see cref="SoftwareBitmap"/>, runs recognition, and returns the extracted
/// text plus lightweight metadata (line/word counts, recognizer language).
/// </summary>
public static class RegionOcr {
    /// <summary>
    /// Recognizes text in <paramref name="bitmap"/> using the user's installed OCR language pack(s).
    /// </summary>
    /// <returns>The recognized text and metadata.</returns>
    /// <exception cref="InvalidOperationException">No OCR engine is available for the user profile.</exception>
    public static RegionOcrResult Recognize(Bitmap bitmap) =>
        RecognizeAsync(bitmap).GetAwaiter().GetResult();

    /// <summary>Async variant of <see cref="Recognize"/>.</summary>
    public static async Task<RegionOcrResult> RecognizeAsync(Bitmap bitmap) {
        // CR P2 (PR 334): fail fast for images > OcrEngine.MaxImageDimension with a clear message
        // so caller can surface invalid-argument rather than opaque get-text-exception from deep OCR.
        // Check the source bitmap (pre SoftwareBitmap) and again after decode.
        uint max = OcrEngine.MaxImageDimension;
        if (bitmap.Width > max || bitmap.Height > max) {
            throw new ArgumentException(
                $"OCR image dimensions {bitmap.Width}x{bitmap.Height} exceed OcrEngine.MaxImageDimension={max}. " +
                "Shrink the get-text region (or target a smaller window portion) and retry.",
                nameof(bitmap));
        }

        OcrEngine? engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null) {
            throw new InvalidOperationException(
                "Windows OCR is not available (no OCR language pack installed for the user profile).");
        }

        using SoftwareBitmap softwareBitmap = await ToSoftwareBitmapAsync(bitmap);
        if (softwareBitmap.PixelWidth > max || softwareBitmap.PixelHeight > max) {
            throw new ArgumentException(
                $"OCR image dimensions {softwareBitmap.PixelWidth}x{softwareBitmap.PixelHeight} exceed OcrEngine.MaxImageDimension={max}. " +
                "Shrink the get-text region and retry.",
                nameof(bitmap));
        }

        OcrResult ocr = await engine.RecognizeAsync(softwareBitmap);

        int lineCount = ocr.Lines.Count;
        int wordCount = ocr.Lines.Sum(l => l.Words.Count);
        string text = ocr.Text ?? string.Empty;
        string? language = engine.RecognizerLanguage?.LanguageTag;

        return new RegionOcrResult(text, lineCount, wordCount, language);
    }

    private static async Task<SoftwareBitmap> ToSoftwareBitmapAsync(Bitmap bitmap) {
        using MemoryStream ms = new();
        bitmap.Save(ms, ImageFormat.Png);
        ms.Position = 0;

        using IRandomAccessStream stream = ms.AsRandomAccessStream();
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync();
    }
}