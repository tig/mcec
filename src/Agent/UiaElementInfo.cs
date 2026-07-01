// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Text.Json.Nodes;

namespace MCEControl;

/// <summary>
/// A single node in a UI Automation (UIA) tree snapshot, captured by <see cref="UiaService"/> and
/// returned to an agent so it can reason over a window's controls (control type, name, automation id,
/// bounds, value, ...) and target them by <c>find</c>/<c>invoke</c>. Optional string properties are
/// omitted from the JSON when empty so the model isn't flooded with blank fields.
/// </summary>
public sealed class UiaElementInfo {
    public string ControlType { get; set; } = "";
    public string? Name { get; set; }
    public string? AutomationId { get; set; }
    public string? ClassName { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsOffscreen { get; set; }
    public string? Value { get; set; }
    public bool? IsSelected { get; set; }
    public List<UiaElementInfo> Children { get; } = [];

    /// <summary>Serializes this node (and its children) to a compact, camelCase JSON object.</summary>
    public JsonObject ToJsonObject() {
        JsonObject obj = new() {
            ["controlType"] = ControlType,
            ["x"] = X,
            ["y"] = Y,
            ["width"] = Width,
            ["height"] = Height,
            ["isEnabled"] = IsEnabled,
            ["isOffscreen"] = IsOffscreen,
        };
        if (Name is not null) {
            obj["name"] = Name;
        }
        if (AutomationId is not null) {
            obj["automationId"] = AutomationId;
        }
        if (ClassName is not null) {
            obj["className"] = ClassName;
        }
        if (Value is not null) {
            obj["value"] = Value;
        }
        if (IsSelected is not null) {
            obj["isSelected"] = IsSelected;
        }

        JsonArray children = [];
        foreach (UiaElementInfo child in Children) {
            children.Add(child.ToJsonObject());
        }
        obj["children"] = children;
        return obj;
    }
}
