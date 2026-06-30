using System;
using Xunit;
using WindowsInput;
using WindowsInput.Native;

namespace MCEControl.xUnit.WindowsInput;

public class KeyboardSimulatorTests
{
    [DesktopInputFact]
    public void Constructor_InitializesSimulator()
    {
        var simulator = new InputSimulator();
        var keyboard = simulator.Keyboard;
        Assert.NotNull(keyboard);
    }

    [DesktopInputFact]
    public void KeyDown_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Keyboard.KeyDown(VirtualKeyCode.VK_A));
        Assert.Null(exception);
    }

    [DesktopInputFact]
    public void KeyUp_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Keyboard.KeyUp(VirtualKeyCode.VK_A));
        Assert.Null(exception);
    }

    [DesktopInputFact]
    public void KeyPress_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Keyboard.KeyPress(VirtualKeyCode.VK_A));
        Assert.Null(exception);
    }

    [DesktopInputFact]
    public void TextEntry_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Keyboard.TextEntry("test"));
        Assert.Null(exception);
    }

    [DesktopInputFact]
    public void TextEntry_WithEmptyString_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Keyboard.TextEntry(""));
        Assert.Null(exception);
    }

    [DesktopInputFact]
    public void ModifiedKeyStroke_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.LCONTROL, VirtualKeyCode.VK_C));
        Assert.Null(exception);
    }

    [DesktopInputFact]
    public void ModifiedKeyStroke_WithMultipleModifiers_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Keyboard.ModifiedKeyStroke(
            new[] { VirtualKeyCode.LCONTROL, VirtualKeyCode.LSHIFT },
            VirtualKeyCode.VK_A
        ));
        Assert.Null(exception);
    }
}
