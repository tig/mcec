// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using Xunit;

namespace MCEControl.xUnit.WindowsInput;

/// <summary>
/// A <see cref="FactAttribute"/> for tests that dispatch REAL keyboard/mouse input through
/// <c>SendInput</c>. They move the cursor and type into whatever window currently has focus — i.e.
/// the terminal running <c>dotnet test</c> — which disrupts the desktop. These tests are therefore
/// skipped by default and only run when <c>MCEC_DESKTOP_E2E=1</c> is set (the same opt-in as the
/// desktop end-to-end test), so a normal <c>dotnet test</c> never injects input.
/// </summary>
public sealed class DesktopInputFactAttribute : FactAttribute {
    public DesktopInputFactAttribute() {
        if (Environment.GetEnvironmentVariable("MCEC_DESKTOP_E2E") != "1") {
            Skip = "Dispatches real keyboard/mouse input into the active desktop; set MCEC_DESKTOP_E2E=1 to run.";
        }
    }
}
