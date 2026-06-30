// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;

namespace MCEControl;

/// <summary>
/// Structured metadata for a top-level window, returned by <see cref="WindowResolver"/> and embedded
/// in agent command results so a model can target a window by handle/title/process/class.
/// </summary>
public sealed class WindowInfo {
    public long Handle { get; set; }
    public string Title { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public int ProcessId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public JsonObject ToJsonObject() => new() {
        ["handle"] = Handle,
        ["title"] = Title,
        ["className"] = ClassName,
        ["processName"] = ProcessName,
        ["processId"] = ProcessId,
        ["x"] = X,
        ["y"] = Y,
        ["width"] = Width,
        ["height"] = Height,
    };
}
