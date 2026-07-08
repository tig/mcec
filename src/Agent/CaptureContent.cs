// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;

namespace MCEControl;

/// <summary>
/// Helpers for capture payload shaping: optional path-only mode (artifact path instead of inline
/// base64) and, when inline bytes are present, projection to an MCP <c>image</c> content block.
/// </summary>
public static class CaptureContent {
    private static int _artifactCounter;

    /// <summary>
    /// Applies path-only capture behavior to <paramref name="result"/> when requested by tool args:
    /// writes the image bytes to the routed session's artifact directory, records the artifact path,
    /// and removes inline base64 from the payload.
    /// </summary>
    public static void ApplyPathOnlyMode(JsonObject? result, JsonObject args, AgentSession session) {
        if (!WantsInlineImage(args) && result?["base64"] is JsonValue v && v.TryGetValue(out string? base64) && !string.IsNullOrEmpty(base64)) {
            string ext = result["encoding"] is JsonValue ev && ev.TryGetValue(out string? enc) && !string.IsNullOrWhiteSpace(enc)
                ? enc.ToLowerInvariant()
                : "png";
            if (TryWriteArtifact(session, base64, ext) is { } path) {
                result["artifact"] = path;
            }
            else {
                result["artifactError"] = "Could not write capture bytes to the session artifact directory.";
            }
            result.Remove("base64");
        }
    }

    /// <summary>True when capture should keep inline bytes and emit an MCP image block.</summary>
    public static bool WantsInlineImage(JsonObject args) {
        bool pathOnly = Bool(args, "pathOnly");
        bool? returnImage = NullableBool(args, "returnImage");
        return !pathOnly && returnImage != false;
    }

    /// <summary>
    /// Returns an MCP image content block for <paramref name="result"/> when it carries a base64 PNG, or
    /// null otherwise. The <paramref name="result"/> object is left unmodified.
    /// </summary>
    public static JsonObject? TryBuildImageBlock(JsonObject? result) {
        if (result?["base64"] is JsonValue v && v.TryGetValue(out string? data)) {
            return new JsonObject {
                ["type"] = "image",
                ["data"] = data,
                ["mimeType"] = "image/png",
            };
        }
        return null;
    }

    /// <summary>Validates downscale/path-only arguments for capture.</summary>
    public static string? ValidateArgs(JsonObject args) {
        if (args.ContainsKey("maxWidth")) {
            if (args["maxWidth"] is not JsonValue mv || !mv.TryGetValue(out int maxWidth) || maxWidth <= 0) {
                return "capture maxWidth must be a positive integer when provided.";
            }
        }
        if (args.ContainsKey("scale")) {
            if (args["scale"] is not JsonValue sv || !sv.TryGetValue(out double scale) || scale <= 0 || scale > 1) {
                return "capture scale must be a number in the range (0, 1] when provided.";
            }
        }
        return null;
    }

    private static bool Bool(JsonObject a, string key) =>
        a[key] is JsonValue v && v.TryGetValue(out bool b) && b;

    private static bool? NullableBool(JsonObject a, string key) =>
        a[key] is JsonValue v && v.TryGetValue(out bool b) ? b : null;

    private static string? TryWriteArtifact(AgentSession session, string base64, string extension) {
        try {
            byte[] bytes = Convert.FromBase64String(base64);
            string name = string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"capture-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Interlocked.Increment(ref _artifactCounter)}.{extension}");
            string path = Path.Combine(session.EnsureArtifactDir(), name);
            File.WriteAllBytes(path, bytes);
            return path;
        }
        catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException) {
            Logger.Instance.Log4.Warn($"CaptureContent: could not write capture artifact: {e.Message}");
            return null;
        }
    }
}
