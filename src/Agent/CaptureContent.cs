// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;

namespace MCEControl;

/// <summary>
/// Bridges a <c>capture</c> result payload to the MCP image content transport. Per the result contract,
/// the PNG is <b>additionally</b> emitted as an MCP <c>image</c> content block (so image-aware clients
/// render it) while the same bytes stay referenced from the envelope's <c>result</c> for text-only
/// agents — so this builds the image block but does <b>not</b> strip <c>base64</c> from the result.
/// </summary>
public static class CaptureContent {
    /// <summary>
    /// Returns an MCP image content block for <paramref name="result"/> when it carries a base64 PNG, or
    /// null otherwise. The <paramref name="result"/> object is left unmodified.
    /// </summary>
    public static JsonObject? TryBuildImageBlock(JsonObject? result) {
        if (result?["base64"] is JsonValue v && v.TryGetValue(out string? data) && data is not null) {
            return new JsonObject {
                ["type"] = "image",
                ["data"] = data,
                ["mimeType"] = "image/png",
            };
        }
        return null;
    }
}
