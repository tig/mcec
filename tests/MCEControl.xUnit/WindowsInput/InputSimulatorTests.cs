using System;
using Xunit;
using WindowsInput;
using WindowsInput.Native;

namespace MCEControl.xUnit.WindowsInput;

public class InputSimulatorTests
{
    [DesktopInputFact]
    public void Constructor_InitializesSimulator()
    {
        var simulator = new InputSimulator();
        Assert.NotNull(simulator);
    }

    [DesktopInputFact]
    public void Keyboard_PropertyNotNull()
    {
        var simulator = new InputSimulator();
        Assert.NotNull(simulator.Keyboard);
    }

    [DesktopInputFact]
    public void Mouse_PropertyNotNull()
    {
        var simulator = new InputSimulator();
        Assert.NotNull(simulator.Mouse);
    }

    [DesktopInputFact]
    public void InputDeviceState_PropertyNotNull()
    {
        var simulator = new InputSimulator();
        Assert.NotNull(simulator.InputDeviceState);
    }
}
