using System;
using Xunit;
using WindowsInput;
using WindowsInput.Native;

namespace MCEControl.xUnit.WindowsInput;

public class InputSimulatorTests
{
    [Fact]
    public void Constructor_InitializesSimulator()
    {
        var simulator = new InputSimulator();
        Assert.NotNull(simulator);
    }

    [Fact]
    public void Keyboard_PropertyNotNull()
    {
        var simulator = new InputSimulator();
        Assert.NotNull(simulator.Keyboard);
    }

    [Fact]
    public void Mouse_PropertyNotNull()
    {
        var simulator = new InputSimulator();
        Assert.NotNull(simulator.Mouse);
    }

    [Fact]
    public void InputDeviceState_PropertyNotNull()
    {
        var simulator = new InputSimulator();
        Assert.NotNull(simulator.InputDeviceState);
    }
}

public class KeyboardSimulatorTests
{
    [Fact]
    public void Constructor_InitializesSimulator()
    {
        var simulator = new InputSimulator();
        var keyboard = simulator.Keyboard;
        Assert.NotNull(keyboard);
    }

    [Fact]
    public void KeyDown_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Keyboard.KeyDown(VirtualKeyCode.VK_A));
        Assert.Null(exception);
    }

    [Fact]
    public void KeyUp_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Keyboard.KeyUp(VirtualKeyCode.VK_A));
        Assert.Null(exception);
    }

    [Fact]
    public void KeyPress_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Keyboard.KeyPress(VirtualKeyCode.VK_A));
        Assert.Null(exception);
    }

    [Fact]
    public void TextEntry_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Keyboard.TextEntry("test"));
        Assert.Null(exception);
    }

    [Fact]
    public void TextEntry_WithEmptyString_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Keyboard.TextEntry(""));
        Assert.Null(exception);
    }

    [Fact]
    public void ModifiedKeyStroke_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.LCONTROL, VirtualKeyCode.VK_C));
        Assert.Null(exception);
    }

    [Fact]
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

public class MouseSimulatorTests
{
    [Fact]
    public void Constructor_InitializesSimulator()
    {
        var simulator = new InputSimulator();
        var mouse = simulator.Mouse;
        Assert.NotNull(mouse);
    }

    [Fact]
    public void LeftButtonClick_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Mouse.LeftButtonClick());
        Assert.Null(exception);
    }

    [Fact]
    public void LeftButtonDown_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Mouse.LeftButtonDown());
        Assert.Null(exception);
    }

    [Fact]
    public void LeftButtonUp_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Mouse.LeftButtonUp());
        Assert.Null(exception);
    }

    [Fact]
    public void LeftButtonDoubleClick_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Mouse.LeftButtonDoubleClick());
        Assert.Null(exception);
    }

    [Fact]
    public void RightButtonClick_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Mouse.RightButtonClick());
        Assert.Null(exception);
    }

    [Fact]
    public void MoveMouseBy_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Mouse.MoveMouseBy(10, 20));
        Assert.Null(exception);
    }

    [Fact]
    public void MoveMouseTo_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Mouse.MoveMouseTo(100.0, 200.0));
        Assert.Null(exception);
    }

    [Fact]
    public void VerticalScroll_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Mouse.VerticalScroll(1));
        Assert.Null(exception);
    }

    [Fact]
    public void HorizontalScroll_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Mouse.HorizontalScroll(1));
        Assert.Null(exception);
    }
}
