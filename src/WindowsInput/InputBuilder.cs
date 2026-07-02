using System;
using System.Collections;
using System.Collections.Generic;
using WindowsInput.Native;

namespace WindowsInput; 
/// <summary>
/// A helper class for building a list of <see cref="INPUT"/> messages ready to be sent to the native Windows API.
/// </summary>
internal class InputBuilder : IEnumerable<INPUT> {
    /// <summary>
    /// The internal list of <see cref="INPUT"/> messages being built by this instance.
    /// </summary>
    private readonly List<INPUT> _inputList;

    /// <summary>
    /// Initializes a new instance of the <see cref="InputBuilder"/> class.
    /// </summary>
    public InputBuilder() {
        _inputList = [];
    }

    /// <summary>
    /// Returns the list of <see cref="INPUT"/> messages as a <see cref="System.Array"/> of <see cref="INPUT"/> messages.
    /// </summary>
    /// <returns>The <see cref="System.Array"/> of <see cref="INPUT"/> messages.</returns>
    public INPUT[] ToArray() {
        return [.. _inputList];
    }

    /// <summary>
    /// Returns an enumerator that iterates through the list of <see cref="INPUT"/> messages.
    /// </summary>
    /// <returns>
    /// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the list of <see cref="INPUT"/> messages.
    /// </returns>
    /// <filterpriority>1</filterpriority>
    public IEnumerator<INPUT> GetEnumerator() {
        return _inputList.GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the list of <see cref="INPUT"/> messages.
    /// </summary>
    /// <returns>
    /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the list of <see cref="INPUT"/> messages.
    /// </returns>
    /// <filterpriority>2</filterpriority>
    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    /// <summary>
    /// Gets the <see cref="INPUT"/> at the specified position.
    /// </summary>
    /// <value>The <see cref="INPUT"/> message at the specified position.</value>
    public INPUT this[int position] {
        get {
            return _inputList[position];
        }
    }

    /// <summary>
    /// Adds a key down to the list of <see cref="INPUT"/> messages.
    /// </summary>
    /// <param name="keyCode">The <see cref="VirtualKeyCode"/>.</param>
    /// <returns>This <see cref="InputBuilder"/> instance.</returns>
    public InputBuilder AddKeyDown(VirtualKeyCode keyCode) {
        INPUT down = new INPUT {
            Type = (UInt32)InputType.Keyboard,
            Data = {
                Keyboard = new KEYBDINPUT {
                    KeyCode = (UInt16)keyCode,
                    Scan = 0,
                    Flags = 0,
                    Time = 0,
                    ExtraInfo = IntPtr.Zero
                }
            }
        };

        _inputList.Add(down);
        return this;
    }

    /// <summary>
    /// Adds a key up to the list of <see cref="INPUT"/> messages.
    /// </summary>
    /// <param name="keyCode">The <see cref="VirtualKeyCode"/>.</param>
    /// <returns>This <see cref="InputBuilder"/> instance.</returns>
    public InputBuilder AddKeyUp(VirtualKeyCode keyCode) {
        INPUT up = new INPUT {
            Type = (UInt32)InputType.Keyboard,
            Data = {
                Keyboard = new KEYBDINPUT {
                    KeyCode = (UInt16)keyCode,
                    Scan = 0,
                    Flags = (UInt32)KeyboardFlag.KeyUp,
                    Time = 0,
                    ExtraInfo = IntPtr.Zero
                }
            }
        };

        _inputList.Add(up);
        return this;
    }

    /// <summary>
    /// Adds a key press to the list of <see cref="INPUT"/> messages which is equivalent to a key down followed by a key up.
    /// </summary>
    /// <param name="keyCode">The <see cref="VirtualKeyCode"/>.</param>
    /// <returns>This <see cref="InputBuilder"/> instance.</returns>
    public InputBuilder AddKeyPress(VirtualKeyCode keyCode) {
        AddKeyDown(keyCode);
        AddKeyUp(keyCode);
        return this;
    }

    /// <summary>
    /// Adds the character to the list of <see cref="INPUT"/> messages.
    /// </summary>
    /// <param name="character">The <see cref="System.Char"/> to be added to the list of <see cref="INPUT"/> messages.</param>
    private void AddCharacter(char character) {
        UInt16 scanCode = character;

        INPUT down = new INPUT {
            Type = (UInt32)InputType.Keyboard,
            Data = {
                Keyboard = new KEYBDINPUT {
                    KeyCode = 0,
                    Scan = scanCode,
                    Flags = (UInt32)KeyboardFlag.Unicode,
                    Time = 0,
                    ExtraInfo = IntPtr.Zero
                }
            }
        };

        INPUT up = new INPUT {
            Type = (UInt32)InputType.Keyboard,
            Data = {
                Keyboard = new KEYBDINPUT {
                    KeyCode = 0,
                    Scan = scanCode,
                    Flags = (UInt32)(KeyboardFlag.KeyUp | KeyboardFlag.Unicode),
                    Time = 0,
                    ExtraInfo = IntPtr.Zero
                }
            }
        };

        // Handle extended keys:
        // If the scan code is preceded by a prefix byte that has the value 0xE0 (224),
        // we need to include the KEYEVENTF_EXTENDEDKEY flag in the Flags property. 
        if ((scanCode & 0xFF00) == 0xE000) {
            down.Data.Keyboard.Flags |= (UInt32)KeyboardFlag.ExtendedKey;
            up.Data.Keyboard.Flags |= (UInt32)KeyboardFlag.ExtendedKey;
        }

        _inputList.Add(down);
        _inputList.Add(up);
    }

    /// <summary>
    /// Adds all of the characters in the specified <see cref="IEnumerable{T}"/> of <see cref="char"/>.
    /// </summary>
    /// <param name="characters">The characters to add.</param>
    /// <returns>This <see cref="InputBuilder"/> instance.</returns>
    private InputBuilder AddCharacters(IEnumerable<char> characters) {
        foreach (char character in characters) {
            AddCharacter(character);
        }
        return this;
    }

    /// <summary>
    /// Adds the characters in the specified <see cref="string"/>.
    /// </summary>
    /// <param name="characters">The string of <see cref="char"/> to add.</param>
    /// <returns>This <see cref="InputBuilder"/> instance.</returns>
    public InputBuilder AddCharacters(string characters) {
        return AddCharacters(characters.ToCharArray());
    }

    #region Mouse

    public InputBuilder AddRelativeMouseMovement(int x, int y) {
        INPUT movement = new INPUT {
            Type = (UInt32)InputType.Mouse,
            Data = { Mouse = { Flags = (UInt32)MouseFlag.Move, X = x, Y = y } }
        };

        _inputList.Add(movement);

        return this;
    }

    public InputBuilder AddAbsoluteMouseMovement(int absoluteX, int absoluteY) {
        INPUT movement = new INPUT {
            Type = (UInt32)InputType.Mouse,
            Data = { Mouse = { Flags = (UInt32)(MouseFlag.Move | MouseFlag.Absolute), X = absoluteX, Y = absoluteY } }
        };

        _inputList.Add(movement);

        return this;
    }

    public InputBuilder AddAbsoluteMouseMovementOnVirtualDesktop(int absoluteX, int absoluteY) {
        INPUT movement = new INPUT {
            Type = (UInt32)InputType.Mouse,
            Data = { Mouse = { Flags = (UInt32)(MouseFlag.Move | MouseFlag.Absolute | MouseFlag.VirtualDesk), X = absoluteX, Y = absoluteY } }
        };

        _inputList.Add(movement);

        return this;
    }

    public InputBuilder AddMouseButtonDown(MouseButton button) {
        INPUT buttonDown = new INPUT {
            Type = (UInt32)InputType.Mouse,
            Data = { Mouse = { Flags = (UInt32)button.ToMouseButtonDownFlag() } }
        };

        _inputList.Add(buttonDown);

        return this;
    }

    public InputBuilder AddMouseXButtonDown(int xButtonId) {
        INPUT buttonDown = new INPUT {
            Type = (UInt32)InputType.Mouse,
            Data = { Mouse = { Flags = (UInt32)MouseFlag.XDown, MouseData = (UInt32)xButtonId } }
        };
        _inputList.Add(buttonDown);

        return this;
    }

    public InputBuilder AddMouseButtonUp(MouseButton button) {
        INPUT buttonUp = new INPUT {
            Type = (UInt32)InputType.Mouse,
            Data = { Mouse = { Flags = (UInt32)button.ToMouseButtonUpFlag() } }
        };
        _inputList.Add(buttonUp);

        return this;
    }

    public InputBuilder AddMouseXButtonUp(int xButtonId) {
        INPUT buttonUp = new INPUT {
            Type = (UInt32)InputType.Mouse,
            Data = { Mouse = { Flags = (UInt32)MouseFlag.XUp, MouseData = (UInt32)xButtonId } }
        };
        _inputList.Add(buttonUp);

        return this;
    }

    public InputBuilder AddMouseButtonClick(MouseButton button) {
        return AddMouseButtonDown(button).AddMouseButtonUp(button);
    }

    public InputBuilder AddMouseXButtonClick(int xButtonId) {
        return AddMouseXButtonDown(xButtonId).AddMouseXButtonUp(xButtonId);
    }

    public InputBuilder AddMouseButtonDoubleClick(MouseButton button) {
        return AddMouseButtonClick(button).AddMouseButtonClick(button);
    }

    public InputBuilder AddMouseXButtonDoubleClick(int xButtonId) {
        return AddMouseXButtonClick(xButtonId).AddMouseXButtonClick(xButtonId);
    }

    public InputBuilder AddMouseVerticalWheelScroll(int scrollAmount) {
        INPUT scroll = new INPUT {
            Type = (UInt32)InputType.Mouse,
            Data = { Mouse = { Flags = (UInt32)MouseFlag.VerticalWheel, MouseData = (UInt32)scrollAmount } }
        };

        _inputList.Add(scroll);

        return this;
    }

    public InputBuilder AddMouseHorizontalWheelScroll(int scrollAmount) {
        INPUT scroll = new INPUT {
            Type = (UInt32)InputType.Mouse,
            Data = { Mouse = { Flags = (UInt32)MouseFlag.HorizontalWheel, MouseData = (UInt32)scrollAmount } }
        };

        _inputList.Add(scroll);

        return this;
    }

    #endregion
}
