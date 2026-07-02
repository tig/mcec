// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Windows.Forms;
using MCEControl;
using Xunit;

namespace MCEControl.xUnit.Dialogs;

/// <summary>
/// Headless tests for the #213 Settings tab UserControls: constructing a WinForms UserControl,
/// calling <see cref="ISettingsTab.Bind"/>, and mutating control text needs no message pump, so
/// these verify the wiring end-to-end — Bind populates from the clone, change handlers mutate
/// ONLY the bound clone (keeping the last good value on unparsable input), and
/// <see cref="ISettingsTab.IsValid"/>/<see cref="ISettingsTab.ValidityChanged"/> track every
/// change.
/// </summary>
public class SettingsTabControlTests {
    private static T FindControl<T>(Control root, string name) where T : Control {
        return (T)root.Controls.Find(name, searchAllChildren: true)[0];
    }

    [Fact]
    public void ClientTab_BindPopulatesAndTracksValidity() {
        var settings = new AppSettings {
            ActAsClient = true,
            ClientHost = "myhost",
            ClientPort = 5150,
        };

        using var tab = new ClientSettingsTab();
        tab.Bind(settings);

        var host = FindControl<TextBox>(tab, "_editClientHost");
        var port = FindControl<TextBox>(tab, "_editClientPort");
        Assert.Equal("myhost", host.Text);
        Assert.Equal("5150", port.Text);
        Assert.True(tab.IsValid);

        int changes = 0;
        tab.ValidityChanged += (_, _) => changes++;

        // Unparsable port: section invalid, but the bound clone keeps the last good value.
        port.Text = "abc";
        Assert.False(tab.IsValid);
        Assert.False(tab.ValidateSection());
        Assert.Equal(5150, settings.ClientPort);
        Assert.True(changes > 0);

        // Fixing the port re-validates and now mutates the clone.
        port.Text = "5151";
        Assert.True(tab.IsValid);
        Assert.Equal(5151, settings.ClientPort);

        // Emptying the host invalidates the section.
        host.Text = "";
        Assert.False(tab.IsValid);

        // Disabling the client makes the section valid regardless.
        var enable = FindControl<CheckBox>(tab, "_checkBoxEnableClient");
        enable.Checked = false;
        Assert.True(tab.IsValid);
        Assert.False(settings.ActAsClient);
    }

    [Fact]
    public void ServerTab_BindPopulatesAndValidatesWakeupSection() {
        var settings = new AppSettings {
            ActAsServer = true,
            ServerPort = 5150,
            WakeupEnabled = true,
            WakeupHost = "wakehost",
            WakeupPort = 3000,
            WakeupCommand = "wake",
            ClosingCommand = "close",
        };

        using var tab = new ServerSettingsTab();
        tab.Bind(settings);
        Assert.True(tab.IsValid);

        // Empty wakeup command invalidates the section...
        var wakeCmd = FindControl<TextBox>(tab, "_editWakeupCommand");
        wakeCmd.Text = "";
        Assert.False(tab.IsValid);

        // ...but turning wakeup off makes it valid again (rule only applies when both enabled).
        var wakeup = FindControl<CheckBox>(tab, "_checkBoxEnableWakeup");
        wakeup.Checked = false;
        Assert.True(tab.IsValid);
        Assert.False(settings.WakeupEnabled);

        // Server port changes flow into the bound clone.
        FindControl<TextBox>(tab, "_editServerPort").Text = "5152";
        Assert.Equal(5152, settings.ServerPort);
    }

    [Fact]
    public void ActivityMonitorTab_BindPopulatesAndTracksValidity() {
        var settings = new AppSettings {
            ActivityMonitorEnabled = true,
            ActivityMonitorCommand = "activity",
            ActivityMonitorDebounceTime = 10,
        };

        using var tab = new ActivityMonitorSettingsTab();
        tab.Bind(settings);
        Assert.True(tab.IsValid);

        var debounce = FindControl<TextBox>(tab, "_textBoxDebounceTime");
        debounce.Text = "0";
        Assert.False(tab.IsValid);
        Assert.Equal(0, settings.ActivityMonitorDebounceTime);

        debounce.Text = "30";
        Assert.True(tab.IsValid);
        Assert.Equal(30, settings.ActivityMonitorDebounceTime);
    }

    [Fact]
    public void SerialTab_BindPopulatesAndMutatesOnlyTheClone() {
        var settings = new AppSettings {
            ActAsSerialServer = true,
            SerialServerPortName = "COM3",
            SerialServerBaudRate = 9600,
        };

        using var tab = new SerialSettingsTab();
        tab.Bind(settings);

        // Serial tab has no invalid states (all drop-downs).
        Assert.True(tab.IsValid);
        Assert.True(tab.ValidateSection());

        var portCombo = FindControl<ComboBox>(tab, "_comboBoxSerialPort");
        Assert.Equal("COM3", portCombo.SelectedItem);

        portCombo.SelectedItem = "COM5";
        Assert.Equal("COM5", settings.SerialServerPortName);
    }

    [Fact]
    public void ClientTab_ChangeHandlersMutateOnlyTheBoundClone() {
        var original = new AppSettings { ActAsClient = true, ClientHost = "orig", ClientPort = 5150 };
        var clone = (AppSettings)original.Clone();

        using var tab = new ClientSettingsTab();
        tab.Bind(clone);

        FindControl<TextBox>(tab, "_editClientHost").Text = "changed";

        // Only the bound clone changes — Cancel semantics depend on this.
        Assert.Equal("changed", clone.ClientHost);
        Assert.Equal("orig", original.ClientHost);
    }
}
