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
