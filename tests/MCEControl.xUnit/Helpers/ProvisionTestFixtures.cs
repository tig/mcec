// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.IO;
using MCEControl;

namespace MCEControl.xUnit.Helpers;

/// <summary>
/// Shared fake-install layout for tests that point <see cref="SessionProvisioner.BinariesDir"/> at a
/// minimal directory. Must include <see cref="SessionProvisioner.RequiredAgentAssemblies"/> (#317).
/// </summary>
public static class ProvisionTestFixtures {
    public static void SeedMinimalInstall(string installDir) {
        Directory.CreateDirectory(installDir);
        File.WriteAllText(Path.Combine(installDir, "mcec.exe"), "stub");
        foreach (string dep in SessionProvisioner.RequiredAgentAssemblies) {
            File.WriteAllText(Path.Combine(installDir, dep), "stub");
        }
    }
}