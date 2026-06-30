// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json;
using System.Text.Json.Serialization;

namespace MCEControl;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> and helpers for the agent (MCEC 3.0) subsystem so
/// every structured reply (capture/query/find/invoke and the MCP/HTTP façade) uses the same JSON
/// shape: camelCase names, nulls omitted, compact by default.
/// </summary>
public static class AgentJson {
    public static JsonSerializerOptions Options { get; } = new JsonSerializerOptions {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
