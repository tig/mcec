using System.Text;
using MCEControl;

namespace MCEControl.xUnit.Services;

/// <summary>A trivial <see cref="Reply"/> for driving the typed CommandReceived event (#211)
/// without a live transport; captures whatever is written back.</summary>
internal sealed class TestReply : Reply {
    private StringBuilder Written { get; } = new();

    public override void Write(string text) {
        Written.Append(text);
    }
}
