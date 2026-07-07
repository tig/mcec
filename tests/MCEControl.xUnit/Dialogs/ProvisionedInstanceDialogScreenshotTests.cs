// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using MCEControl;
using Xunit;

namespace MCEControl.xUnit.Dialogs;

/// <summary>
/// Opt-in doc asset export for <see cref="ProvisionedInstanceDialog"/>. Run with
/// <c>MCEC_DOC_SCREENSHOT=1</c> to write <c>docs/provision_handoff.png</c>; CI leaves it a no-op.
/// </summary>
public class ProvisionedInstanceDialogScreenshotTests {
    [Fact]
    public void ExportHandoffDialog_WritesProvisionHandoffPngWhenOptedIn() {
        if (Environment.GetEnvironmentVariable("MCEC_DOC_SCREENSHOT") != "1") {
            return;
        }

        ProvisionedSession session = new() {
            SessionId = "0123456789ab",
            Directory = @"C:\Users\You\AppData\Local\MCEC\sessions\0123456789ab",
            ExePath = @"C:\Users\You\AppData\Local\MCEC\sessions\0123456789ab\mcec.exe",
            McpServerEnabled = true,
            BindAddress = "127.0.0.1",
            Port = 51515,
            Token = "0123456789abcdef0123456789abcdef",
        };

        using ProvisionedInstanceDialog dialog = new(session);
        dialog.Show();
        dialog.Refresh();
        Application.DoEvents();

        using Bitmap bitmap = new(dialog.Width, dialog.Height);
        dialog.DrawToBitmap(bitmap, new Rectangle(0, 0, dialog.Width, dialog.Height));

        string docs = FindDocsDirectory();
        string path = Path.Combine(docs, "provision_handoff.png");
        bitmap.Save(path, ImageFormat.Png);
        Assert.True(File.Exists(path));
    }

    private static string FindDocsDirectory() {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null) {
            string candidate = Path.Combine(dir, "docs");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "_config.yml"))) {
                return candidate;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("Could not locate docs/ (expected _config.yml alongside).");
    }
}