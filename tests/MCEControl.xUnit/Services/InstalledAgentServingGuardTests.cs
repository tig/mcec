// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.IO;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Services;

/// <summary>
/// SECURITY: the installed (Program Files) copy of MCEC must never serve agents; serving from it
/// would require enabling agent security gates in the operator's own configuration, which a crashed
/// session leaks enabled. These tests cover the shared detection (<see cref="Program.GetProgramFilesRoot"/>)
/// and the MCP/HTTP refusal (<see cref="AgentServer.StartHttp"/> via the test seam). The <c>--mcp</c>
/// refusal shares the same <see cref="Program.IsProgramFilesInstall"/> check. Mutates the process-global
/// seam, so serialized via the AgentSerial collection.
/// </summary>
[Collection("AgentSerial")]
public class InstalledAgentServingGuardTests {
    [Fact]
    public void GetProgramFilesRoot_InstalledPath_ReturnsRoot() {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        Assert.False(string.IsNullOrEmpty(programFiles), "no Program Files on this machine?");

        string installed = Path.Combine(programFiles, "Kindel Systems", "MCEC") + Path.DirectorySeparatorChar;

        Assert.Equal(programFiles, Program.GetProgramFilesRoot(installed));
    }

    [Fact]
    public void GetProgramFilesRoot_WritableLocations_ReturnNull() {
        Assert.Null(Program.GetProgramFilesRoot(Path.Combine(Path.GetTempPath(), "mcec-session") + Path.DirectorySeparatorChar));
        Assert.Null(Program.GetProgramFilesRoot(@"C:\Users\someone\mcec\"));
    }

    [Fact]
    public void GetProgramFilesRoot_X86Path_ReturnsX86Root_NotThe64BitPrefix() {
        // "C:\Program Files (x86)\..." has "C:\Program Files" as a plain string prefix, so a
        // StartsWith without a directory boundary would report the x86 install under the 64-bit
        // root (and mangle the ConfigPath %AppData% redirect). The x86 root must win.
        string x86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        Assert.False(string.IsNullOrEmpty(x86), "no 32-bit Program Files on this machine?");

        string installed = Path.Combine(x86, "Kindel Systems", "MCEC") + Path.DirectorySeparatorChar;

        Assert.Equal(x86, Program.GetProgramFilesRoot(installed));
    }

    [Fact]
    public void GetProgramFilesRoot_PrefixSiblingDir_IsNotAMatch() {
        // A directory whose name merely BEGINS with the root (no separator boundary) is a sibling,
        // not a child: "C:\Program FilesExtra\..." is not installed under "C:\Program Files".
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        Assert.False(string.IsNullOrEmpty(programFiles));

        string sibling = programFiles + "Extra" + Path.DirectorySeparatorChar + "App" + Path.DirectorySeparatorChar;

        Assert.Null(Program.GetProgramFilesRoot(sibling));
    }

    [Fact]
    public void GetProgramFilesRoot_ExactRoot_Matches() {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        Assert.Equal(programFiles, Program.GetProgramFilesRoot(programFiles));
    }

    [Fact]
    public void GetProgramFilesRoot_MatchIsCaseInsensitive() {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string installed = (programFiles + @"\MCEC\").ToUpperInvariant();

        Assert.NotNull(Program.GetProgramFilesRoot(installed));
    }

    [Fact]
    public void StartHttp_FromInstalledLocation_RefusesToListen() {
        try {
            Program.IsProgramFilesInstallOverrideForTests = true;

            AgentServer.StartHttp();

            Assert.False(AgentServer.IsHttpListening, "the installed copy must never serve the MCP/HTTP front door");
        }
        finally {
            Program.IsProgramFilesInstallOverrideForTests = null;
            AgentServer.StopHttp();
        }
    }
}
