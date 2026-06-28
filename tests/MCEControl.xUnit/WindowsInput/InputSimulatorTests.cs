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
