using System;
using System.Collections.Generic;
using System.Linq;
using WindowsInput.Native;

namespace WindowsInput; 
/// <summary>
/// Implements the <see cref="IKeyboardSimulator"/> interface by calling the an <see cref="IInputMessageDispatcher"/> to simulate Keyboard gestures.
/// </summary>
public class KeyboardSimulator : IKeyboardSimulator {
    /// <summary>
    /// The instance of the <see cref="IInputMessageDispatcher"/> to use for dispatching <see cref="INPUT"/> messages.
    /// </summary>
    private readonly IInputMessageDispatcher _messageDispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyboardSimulator"/> class using the specified <see cref="IInputMessageDispatcher"/> for dispatching <see cref="INPUT"/> messages.
    /// </summary>
    /// <param name="messageDispatcher">The <see cref="IInputMessageDispatcher"/> to use for dispatching <see cref="INPUT"/> messages.</param>
    /// <exception cref="InvalidOperationException">If null is passed as the <paramref name="messageDispatcher"/>.</exception>
    public KeyboardSimulator(IInputMessageDispatcher messageDispatcher) {
        _messageDispatcher = messageDispatcher ?? throw new InvalidOperationException(
            $"The {nameof(KeyboardSimulator)} cannot operate with a null {nameof(IInputMessageDispatcher)}. Please provide a valid {nameof(IInputMessageDispatcher)} instance to use for dispatching {nameof(INPUT)} messages.");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyboardSimulator"/> class using an instance of a <see cref="WindowsInputMessageDispatcher"/> for dispatching <see cref="INPUT"/> messages.
    /// </summary>
    public KeyboardSimulator() {
        _messageDispatcher = new WindowsInputMessageDispatcher();
    }

    /// <summary>
    /// Sends the list of <see cref="INPUT"/> messages using the <see cref="IInputMessageDispatcher"/> instance.
    /// </summary>
    /// <param name="inputList">The <see cref="System.Array"/> of <see cref="INPUT"/> messages to send.</param>
    private void SendSimulatedInput(INPUT[] inputList) {
        if (inputList.Length == 0) {
            return;
        }

        _messageDispatcher.DispatchInput(inputList);
    }

    /// <summary>
    /// Calls the Win32 SendInput method to simulate a KeyDown.
    /// </summary>
    /// <param name="keyCode">The <see cref="VirtualKeyCode"/> to press</param>
    public void KeyDown(VirtualKeyCode keyCode) {
        INPUT[] inputList = new InputBuilder().AddKeyDown(keyCode).ToArray();

        SendSimulatedInput(inputList);
    }

    /// <summary>
    /// Calls the Win32 SendInput method to simulate a KeyUp.
    /// </summary>
    /// <param name="keyCode">The <see cref="VirtualKeyCode"/> to lift up</param>
    public void KeyUp(VirtualKeyCode keyCode) {
        INPUT[] inputList = new InputBuilder().AddKeyUp(keyCode).ToArray();
        SendSimulatedInput(inputList);
    }

    /// <summary>
    /// Calls the Win32 SendInput method with a KeyDown and KeyUp message in the same input sequence in order to simulate a Key PRESS.
    /// </summary>
    /// <param name="keyCode">The <see cref="VirtualKeyCode"/> to press</param>
    public void KeyPress(VirtualKeyCode keyCode) {
        INPUT[] inputList =
            new InputBuilder()
                .AddKeyDown(keyCode)
                .AddKeyUp(keyCode)
                .ToArray();

        SendSimulatedInput(inputList);
    }

    /// <summary>
    /// Simulates a simple modified keystroke like CTRL-C where CTRL is the modifierKey and C is the key.
    /// The flow is Modifier KeyDown, Key Press, Modifier KeyUp.
    /// </summary>
    /// <param name="modifierKeyCode">The modifier key</param>
    /// <param name="keyCode">The key to simulate</param>
    public void ModifiedKeyStroke(VirtualKeyCode modifierKeyCode, VirtualKeyCode keyCode) {
        INPUT[] inputList =
            new InputBuilder()
                .AddKeyDown(modifierKeyCode)
                .AddKeyPress(keyCode)
                .AddKeyUp(modifierKeyCode)
                .ToArray();

        SendSimulatedInput(inputList);
    }

    /// <summary>
    /// Simulates a modified keystroke where there are multiple modifiers and one key like CTRL-ALT-C where CTRL and ALT are the modifierKeys and C is the key.
    /// The flow is Modifiers KeyDown in order, Key Press, Modifiers KeyUp in reverse order.
    /// </summary>
    /// <param name="modifierKeyCodes">The list of modifier keys</param>
    /// <param name="keyCode">The key to simulate</param>
    public void ModifiedKeyStroke(IEnumerable<VirtualKeyCode> modifierKeyCodes, VirtualKeyCode keyCode) {
        InputBuilder builder = new InputBuilder();
        List<VirtualKeyCode> modifiers = [.. modifierKeyCodes];
        modifiers.ForEach(x => builder.AddKeyDown(x));

        builder.AddKeyPress(keyCode);
        modifiers.Reverse();
        modifiers.ForEach(x => builder.AddKeyUp(x));

        SendSimulatedInput(builder.ToArray());
    }

    /// <summary>
    /// Simulates a modified keystroke where there is one modifier and multiple keys like CTRL-K-C where CTRL is the modifierKey and K and C are the keys.
    /// The flow is Modifier KeyDown, Keys Press in order, Modifier KeyUp.
    /// </summary>
    /// <param name="modifierKey">The modifier key</param>
    /// <param name="keyCodes">The list of keys to simulate</param>
    public void ModifiedKeyStroke(VirtualKeyCode modifierKey, IEnumerable<VirtualKeyCode> keyCodes) {
        InputBuilder builder = new InputBuilder();
        builder.AddKeyDown(modifierKey);
        keyCodes.ToList().ForEach(x => builder.AddKeyPress(x));

        builder.AddKeyUp(modifierKey);

        SendSimulatedInput(builder.ToArray());
    }

    /// <summary>
    /// Simulates a modified keystroke where there are multiple modifiers and multiple keys like CTRL-ALT-K-C where CTRL and ALT are the modifierKeys and K and C are the keys.
    /// The flow is Modifiers KeyDown in order, Keys Press in order, Modifiers KeyUp in reverse order.
    /// </summary>
    /// <param name="modifierKeyCodes">The list of modifier keys</param>
    /// <param name="keyCodes">The list of keys to simulate</param>
    public void ModifiedKeyStroke(IEnumerable<VirtualKeyCode> modifierKeyCodes, IEnumerable<VirtualKeyCode> keyCodes) {
        InputBuilder builder = new InputBuilder();
        List<VirtualKeyCode> modifiers = [.. modifierKeyCodes];
        modifiers.ForEach(x => builder.AddKeyUp(x));

        keyCodes.ToList().ForEach(x => builder.AddKeyPress(x));

        modifiers.Reverse();
        modifiers.ForEach(x => builder.AddKeyUp(x));

        SendSimulatedInput(builder.ToArray());
    }

    /// <summary>
    /// Calls the Win32 SendInput method with a stream of KeyDown and KeyUp messages in order to simulate uninterrupted text entry via the keyboard.
    /// </summary>
    /// <param name="text">The text to be simulated.</param>
    public void TextEntry(string text) {
        if (text is null) {
            throw new ArgumentNullException(nameof(text));
        }

        //var chars = UTF8Encoding.Unicode.GetBytes(text);

        //var len = chars.Length;
        //INPUT[] inputList = new INPUT[len * 2];
        //for (int x = 0; x < len; x += 2)
        //{
        //    UInt16 scanCode = BitConverter.ToUInt16(chars, x);
        //}
        INPUT[] inputList = new InputBuilder().AddCharacters(text).ToArray();

        SendSimulatedInput(inputList);
    }
}
