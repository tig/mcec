// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Xunit;
using MCEControl;

namespace MCEControl.xUnit.Agent;

/// <summary>
/// Tests for the #209 host seam: no code below the UI layer may touch <c>MainWindow.Instance</c>,
/// which is now explicitly assigned by <c>Program</c>'s GUI path (never lazily constructed). Engine
/// code uses <see cref="AgentRuntime.SendLine"/> / <see cref="AgentRuntime.RequestShutdown"/> /
/// <see cref="AgentRuntime.MessageWindowHandle"/>, all callable headless with no WinForms side
/// effects. Mutates AgentRuntime statics, so serialized via the AgentSerial collection and every
/// test saves/restores in finally.
/// </summary>
[Collection("AgentSerial")]
public class AppHostSeamTests {
    // -------------------------------------------------------------------------------------------
    // MainWindow.Instance: explicit assignment, pointed failure; never a silent Form construction
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void MainWindowInstance_Unassigned_ThrowsPointedException_NotFormConstruction() {
        // In this (headless) test process nothing ever assigns MainWindow.Instance, exactly like
        // --mcp mode. The old Lazy<MainWindow> would CONSTRUCT the Form right here, on an xUnit
        // worker thread; now the touch must fail fast and point at the seam.
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => MainWindow.Instance);

        Assert.Contains("headless", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AgentRuntime", ex.Message, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------------------------
    // AgentRuntime.SendLine / RequestShutdown / MessageWindowHandle
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void SendLine_NoHost_IsLoggedNoOp_NeverThrows() {
        IAppHost? savedHost = AgentRuntime.Host;
        try {
            AgentRuntime.Host = null;

            // Callers include the activity monitor's background dispatch; must never throw.
            Exception? ex = Record.Exception(() => AgentRuntime.SendLine("activity"));

            Assert.Null(ex);
        }
        finally {
            AgentRuntime.Host = savedHost;
        }
    }

    [Fact]
    public void SendLine_ForwardsToRegisteredHost() {
        IAppHost? savedHost = AgentRuntime.Host;
        try {
            RecordingAppHost host = new();
            AgentRuntime.Host = host;

            AgentRuntime.SendLine("activity");

            Assert.Equal(["activity"], host.Lines);
        }
        finally {
            AgentRuntime.Host = savedHost;
        }
    }

    [Fact]
    public void RequestShutdown_NoHost_IsLoggedNoOp_NeverThrows() {
        IAppHost? savedHost = AgentRuntime.Host;
        try {
            AgentRuntime.Host = null;

            // A stray mcec:exit in a hostless process (tests!) must not do anything drastic.
            Exception? ex = Record.Exception(AgentRuntime.RequestShutdown);

            Assert.Null(ex);
        }
        finally {
            AgentRuntime.Host = savedHost;
        }
    }

    [Fact]
    public void RequestShutdown_ForwardsToRegisteredHost() {
        IAppHost? savedHost = AgentRuntime.Host;
        try {
            RecordingAppHost host = new();
            AgentRuntime.Host = host;

            AgentRuntime.RequestShutdown();

            Assert.Equal(1, host.ShutdownRequests);
        }
        finally {
            AgentRuntime.Host = savedHost;
        }
    }

    [Fact]
    public void MessageWindowHandle_NoHost_ThrowsPointedException() {
        IAppHost? savedHost = AgentRuntime.Host;
        try {
            AgentRuntime.Host = null;

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => AgentRuntime.MessageWindowHandle);

            Assert.Contains("IAppHost", ex.Message, StringComparison.Ordinal);
        }
        finally {
            AgentRuntime.Host = savedHost;
        }
    }

    [Fact]
    public void MessageWindowHandle_ForwardsToRegisteredHost() {
        IAppHost? savedHost = AgentRuntime.Host;
        try {
            RecordingAppHost host = new() { Handle = new IntPtr(0xBEEF) };
            AgentRuntime.Host = host;

            Assert.Equal(new IntPtr(0xBEEF), AgentRuntime.MessageWindowHandle);
        }
        finally {
            AgentRuntime.Host = savedHost;
        }
    }

    // -------------------------------------------------------------------------------------------
    // HeadlessAppHost
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void HeadlessHost_SendLine_IsNoOp() {
        Exception? ex = Record.Exception(() => new HeadlessAppHost().SendLine("activity"));

        Assert.Null(ex);
    }

    [Fact]
    public void HeadlessHost_MessageWindowHandle_ThrowsPointedException() {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => ((IAppHost)new HeadlessAppHost()).MessageWindowHandle);

        Assert.Contains("headless", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HeadlessHost_RequestShutdown_ExitsCleanly_AfterGrace() {
        // Swap the process-exit seam so exercising the real shutdown path can't kill the test runner.
        Action<int> savedExit = HeadlessAppHost.ExitProcess;
        int savedGrace = HeadlessAppHost.ShutdownGraceMs;
        CommandInvoker? savedInvoker = AgentRuntime.Invoker;
        using ManualResetEventSlim exited = new(false);
        int? exitCode = null;
        try {
            // The teardown path also stops the ambient invoker; don't stop one another test owns.
            AgentRuntime.Invoker = null;
            HeadlessAppHost.ShutdownGraceMs = 10;
            HeadlessAppHost.ExitProcess = code => { exitCode = code; exited.Set(); };

            new HeadlessAppHost().RequestShutdown();

            // Deferred: exits on a background task after the grace, with a clean (0) exit code.
            Assert.True(exited.Wait(TimeSpan.FromSeconds(10)), "HeadlessAppHost never reached the process-exit call");
            Assert.Equal(0, exitCode);
        }
        finally {
            HeadlessAppHost.ExitProcess = savedExit;
            HeadlessAppHost.ShutdownGraceMs = savedGrace;
            AgentRuntime.Invoker = savedInvoker;
        }
    }

    // -------------------------------------------------------------------------------------------
    // SaveCommands headless guard (#209): a failed save in --mcp mode must log, not block on UI
    // -------------------------------------------------------------------------------------------

    [Fact]
    public void SaveCommands_FailureWhileHeadless_LogsAndReturns_NoDialog() {
        bool savedHeadless = AgentRuntime.Headless;
        try {
            AgentRuntime.Headless = true;

            // A directory that does not exist makes FileStream creation throw; the failure branch
            // that used to show an unconditional MessageBox. Headless, a dialog would block this
            // test forever (nobody can dismiss it), so completing at all is the assertion.
            string unwritable = Path.Combine(Path.GetTempPath(), "mcec-tests", Guid.NewGuid().ToString("N"), "nope", "mcec.commands");
            SerializedCommands commands = new() { commandArray = [] };

            Exception? ex = Record.Exception(() => SerializedCommands.SaveCommands(unwritable, commands, "9.9.9"));

            Assert.Null(ex);
        }
        finally {
            AgentRuntime.Headless = savedHeadless;
        }
    }
}
