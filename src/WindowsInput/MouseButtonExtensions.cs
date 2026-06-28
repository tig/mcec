using WindowsInput.Native;

namespace WindowsInput;
internal static class MouseButtonExtensions {
    internal static MouseFlag ToMouseButtonDownFlag(this MouseButton button) {
        return button switch {
            MouseButton.LeftButton => MouseFlag.LeftDown,
            MouseButton.MiddleButton => MouseFlag.MiddleDown,
            MouseButton.RightButton => MouseFlag.RightDown,
            _ => MouseFlag.LeftDown,
        };
    }

    internal static MouseFlag ToMouseButtonUpFlag(this MouseButton button) {
        return button switch {
            MouseButton.LeftButton => MouseFlag.LeftUp,
            MouseButton.MiddleButton => MouseFlag.MiddleUp,
            MouseButton.RightButton => MouseFlag.RightUp,
            _ => MouseFlag.LeftUp,
        };
    }
}
