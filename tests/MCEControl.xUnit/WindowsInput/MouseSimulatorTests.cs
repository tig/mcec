using System;
using Xunit;
using WindowsInput;
using WindowsInput.Native;

namespace MCEControl.xUnit.WindowsInput;

public class MouseSimulatorTests
{
    [DesktopInputFact]
    public void Constructor_InitializesSimulator()
    {
        var simulator = new InputSimulator();
        var mouse = simulator.Mouse;
        Assert.NotNull(mouse);
    }

    [DesktopInputFact]
    public void LeftButtonClick_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Mouse.LeftButtonClick());
        Assert.Null(exception);
    }

    [DesktopInputFact]
    public void LeftButtonDown_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Mouse.LeftButtonDown());
        Assert.Null(exception);
    }

    [DesktopInputFact]
    public void LeftButtonUp_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Mouse.LeftButtonUp());
        Assert.Null(exception);
    }

    [DesktopInputFact]
    public void LeftButtonDoubleClick_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Mouse.LeftButtonDoubleClick());
        Assert.Null(exception);
    }

    [DesktopInputFact]
    public void RightButtonClick_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Mouse.RightButtonClick());
        Assert.Null(exception);
    }

    [DesktopInputFact]
    public void MoveMouseBy_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Mouse.MoveMouseBy(10, 20));
        Assert.Null(exception);
    }

    [DesktopInputFact]
    public void MoveMouseTo_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Mouse.MoveMouseTo(100.0, 200.0));
        Assert.Null(exception);
    }

    [DesktopInputFact]
    public void VerticalScroll_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Mouse.VerticalScroll(1));
        Assert.Null(exception);
    }

    [DesktopInputFact]
    public void HorizontalScroll_DoesNotThrow()
    {
        var simulator = new InputSimulator();
        var exception = Record.Exception(() => simulator.Mouse.HorizontalScroll(1));
        Assert.Null(exception);
    }
}
