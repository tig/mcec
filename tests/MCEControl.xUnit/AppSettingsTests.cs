using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using Xunit;
using System.IO;

namespace MCEControl.xUnit
{
    public class AppSettingsTests
    {
        [Fact]
        public void GetSettingsPath_inProgramFiles_Test()
        {
            // If we're running within Program Files, use %AppData% 
            var startupPath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}\Kindel Systems\MCE Controller";
            // should be "%appdata%\Kindel Systems\MCE Controller"
            var settingsPath = $@"{Application.UserAppDataPath.Substring(0, Application.UserAppDataPath.Length - (Application.ProductVersion.Length + 1))}";
            Assert.True(AppSettings.GetSettingsPath(startupPath).CompareTo(settingsPath) == 0);
        }

        [Fact]
        public void GetSettingsPath_standalone_Test()
        {
            // If we're running elsewhere (not in Program Files), use current dir
            var startupPath = $@".";
            // should be ame as Application.Startpath
            var settingsPath = $@".";
            Assert.True(AppSettings.GetSettingsPath(startupPath).CompareTo(settingsPath) == 0);
        }

        /// <summary>
        /// Tests that SafeForTelemetryAttribute is working
        /// </summary>
        [Fact]
        public void GetTelemetryDictionary_Test()
        {
            var appSettings = new AppSettings();
            var dict = appSettings.GetTelemetryDictionary();

            Assert.True(dict.ContainsKey("AutoStart"));

            Assert.False(dict.ContainsKey("WakeupCommand"));
        }

        [Fact]
        public void DeserializeExists_Test()
        {
            var tempPath = Path.GetTempPath();
            var settingsPath = AppSettings.GetSettingsPath(tempPath);

            var settings = new AppSettings();
            settings.AutoStart = true; // default is false, so this is a good test

            var settingsFullPath = $@"{tempPath}test.settings.xml";
            settings.Serialize(settingsFullPath);

            string text = System.IO.File.ReadAllText(settingsFullPath);

            Assert.False(string.IsNullOrEmpty(text));

            var readSettings = AppSettings.Deserialize(settingsFullPath);

            Assert.True(readSettings.AutoStart == true);
        }

        [Fact]
        public void DeserializeNotExists_Test()
        {
            // create a temp file and then delete it
            var settingsFullPath = Path.GetTempFileName();
            File.Delete(settingsFullPath);

            // Verify it was deleted
            Assert.False(File.Exists(settingsFullPath));

            var readSettings = AppSettings.Deserialize(settingsFullPath);
            Assert.True(File.Exists(settingsFullPath));

            // This proves default settings were saved
            Assert.Equal("localhost", readSettings.ClientHost);

            File.Delete(settingsFullPath);
            Assert.False(File.Exists(settingsFullPath));
        }
    }
}