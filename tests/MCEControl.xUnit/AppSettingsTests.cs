using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace MCEControl.xUnit
{
    public class AppSettingsTests
    {
        [Fact]
        public void GetSettingsPathTest()
        {
            var startupPathIfProgramFiles = $@"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}\Kindel Systems\MCE Controller";
            // If we're running within Program Files, use %AppData% 
            var settingsPathIfProgramFiles = $@"{Application.UserAppDataPath.Substring(0, Application.UserAppDataPath.Length - (Application.ProductVersion.Length + 1))}";
            Assert.True(AppSettings.GetSettingsPath(startupPathIfProgramFiles).CompareTo(settingsPathIfProgramFiles) == 0);
        }
    }
}
