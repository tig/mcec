// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.IO;
using Microsoft.ApplicationInsights.DataContracts;
using Xunit;

namespace MCEControl.xUnit.Services;

/// <summary>
/// Tests for #156: exception telemetry must not leak the cleartext Windows username via
/// user-profile paths in exception messages/stacks. The scrubber is pure/static so these
/// tests exercise it directly with injected profile/username values (deterministic), plus
/// a few tests against the real environment values.
/// </summary>
public class TelemetryScrubberTests {
    private const string Profile = @"C:\Users\testuser";
    private const string User = "testuser";

    private static string Scrub(string text) => TelemetryScrubber.ScrubUserPaths(text, Profile, User);

    private static Exception Thrown(Exception ex) {
        try {
            throw ex;
        }
        catch (Exception caught) {
            return caught;
        }
    }

    [Theory]
    [InlineData(@"Could not find file 'C:\Users\testuser\AppData\Roaming\MCEControl\MCEControl.settings'.",
                @"Could not find file '%USERPROFILE%\AppData\Roaming\MCEControl\MCEControl.settings'.")]
    // Windows paths are case-insensitive; the scrub must be too.
    [InlineData(@"Access to the path 'c:\users\TESTUSER\AppData\Local\Temp\x.tmp' is denied.",
                @"Access to the path '%USERPROFILE%\AppData\Local\Temp\x.tmp' is denied.")]
    // URI/forward-slash form of the same path.
    [InlineData("file:///C:/Users/testuser/AppData/Roaming/MCEControl/MCEControl.commands",
                "file:///%USERPROFILE%/AppData/Roaming/MCEControl/MCEControl.commands")]
    public void ScrubUserPaths_RedactsUserProfileDirectory(string input, string expected) {
        Assert.Equal(expected, Scrub(input));
    }

    [Theory]
    // Username as a segment of a non-profile path.
    [InlineData(@"D:\Temp\testuser\cache\a.txt", @"D:\Temp\%USERNAME%\cache\a.txt")]
    // Terminal segment (UNC share).
    [InlineData(@"\\server\share\testuser", @"\\server\share\%USERNAME%")]
    // Case-insensitive.
    [InlineData(@"D:\TESTUSER\x", @"D:\%USERNAME%\x")]
    // Relative path starting with the username.
    [InlineData(@"testuser\AppData\Roaming", @"%USERNAME%\AppData\Roaming")]
    // Username as a file base name.
    [InlineData(@"C:\logs\testuser.log", @"C:\logs\%USERNAME%.log")]
    public void ScrubUserPaths_RedactsUserNamePathSegments(string input, string expected) {
        Assert.Equal(expected, Scrub(input));
    }

    [Theory]
    // A different user whose name merely starts with ours must survive intact.
    [InlineData(@"C:\Users\testuser2\file.txt")]
    [InlineData(@"C:\Users\testusers\file.txt")]
    [InlineData(@"D:\Temp\testuserdata\x")]
    // Bare word outside a path context is left alone (don't destroy prose).
    [InlineData("testuser encountered an error")]
    // Ordinary messages and type/method names are untouched.
    [InlineData("Object reference not set to an instance of an object.")]
    [InlineData(@"   at MCEControl.TelemetryService.TrackException(Exception ex)")]
    public void ScrubUserPaths_DoesNotOverRedact(string input) {
        Assert.Equal(input, Scrub(input));
    }

    [Fact]
    public void ScrubUserPaths_StackTraceLine_RedactsPathButKeepsMethodName() {
        string line = @"   at MCEControl.MainWindow.SaveSettings() in C:\Users\testuser\s\mcec\src\MainWindow.cs:line 123";
        string scrubbed = Scrub(line);
        Assert.Contains("MCEControl.MainWindow.SaveSettings()", scrubbed, StringComparison.Ordinal);
        Assert.Contains(@"%USERPROFILE%\s\mcec\src\MainWindow.cs:line 123", scrubbed, StringComparison.Ordinal);
        Assert.DoesNotContain(User, scrubbed, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScrubUserPaths_NullOrEmpty_ReturnsEmpty() {
        Assert.Equal(string.Empty, TelemetryScrubber.ScrubUserPaths(null, Profile, User));
        Assert.Equal(string.Empty, TelemetryScrubber.ScrubUserPaths(string.Empty, Profile, User));
    }

    [Fact]
    public void ScrubUserPaths_NoUserInfoAvailable_ReturnsInputUnchanged() {
        Assert.Equal(@"C:\Users\someone\x", TelemetryScrubber.ScrubUserPaths(@"C:\Users\someone\x", null, null));
    }

    [Fact]
    public void ScrubUserPaths_DefaultOverload_RedactsCurrentEnvironmentUser() {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string input = $@"Access to the path '{profile}\AppData\Roaming\MCEControl\MCEControl.log' is denied.";

        string scrubbed = TelemetryScrubber.ScrubUserPaths(input);

        Assert.DoesNotContain(profile, scrubbed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%USERPROFILE%", scrubbed, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateScrubbedExceptionTelemetry_RedactsMessage_PreservesExceptionType() {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Exception ex = Thrown(new FileNotFoundException(
            $@"Could not find file '{profile}\AppData\Roaming\MCEControl\MCEControl.commands'."));

        ExceptionTelemetry telex = TelemetryScrubber.CreateScrubbedExceptionTelemetry(ex);

        ExceptionDetailsInfo details = Assert.Single(telex.ExceptionDetailsInfoList);
        Assert.Equal(typeof(FileNotFoundException).FullName, details.TypeName);
        Assert.DoesNotContain(profile, details.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%USERPROFILE%", details.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateScrubbedExceptionTelemetry_RedactsInnerExceptionMessages() {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Exception inner = Thrown(new UnauthorizedAccessException(
            $@"Access to the path '{profile}\AppData\Roaming\MCEControl\MCEControl.settings' is denied."));
        Exception outer = Thrown(new InvalidOperationException(
            $@"Failed to load settings from {profile}\AppData\Roaming\MCEControl", inner));

        ExceptionTelemetry telex = TelemetryScrubber.CreateScrubbedExceptionTelemetry(outer);

        Assert.Equal(2, telex.ExceptionDetailsInfoList.Count);
        Assert.All(telex.ExceptionDetailsInfoList, d => {
            Assert.DoesNotContain(profile, d.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("%USERPROFILE%", d.Message, StringComparison.Ordinal);
        });
        Assert.Equal(typeof(InvalidOperationException).FullName, telex.ExceptionDetailsInfoList[0].TypeName);
        Assert.Equal(typeof(UnauthorizedAccessException).FullName, telex.ExceptionDetailsInfoList[1].TypeName);
    }

    [Fact]
    public void CreateScrubbedExceptionTelemetry_AggregateException_IncludesAllInners() {
        Exception ex = Thrown(new AggregateException(
            Thrown(new InvalidOperationException("first")),
            Thrown(new IOException("second"))));

        ExceptionTelemetry telex = TelemetryScrubber.CreateScrubbedExceptionTelemetry(ex);

        Assert.Equal(3, telex.ExceptionDetailsInfoList.Count);
    }

    [Fact]
    public void CreateScrubbedExceptionTelemetry_LongChain_TruncationPreservesRootCause() {
        // 15-deep chain: 14 wrappers around a distinctive root cause. Truncation to the cap (10)
        // must keep the deepest exception (the root cause), not silently drop it, and must say
        // that something was omitted.
        Exception ex = Thrown(new FileNotFoundException("ROOT-CAUSE: the actual failure"));
        for (int i = 0; i < 14; i++) {
            ex = Thrown(new InvalidOperationException($"wrapper {i}", ex));
        }

        ExceptionTelemetry telex = TelemetryScrubber.CreateScrubbedExceptionTelemetry(ex);

        Assert.Equal(10, telex.ExceptionDetailsInfoList.Count);
        // Outermost detail first, unchanged.
        Assert.Equal(typeof(InvalidOperationException).FullName, telex.ExceptionDetailsInfoList[0].TypeName);
        Assert.Contains("wrapper 13", telex.ExceptionDetailsInfoList[0].Message, StringComparison.Ordinal);
        // The leaf (root cause) survives, marked as a truncated chain.
        ExceptionDetailsInfo leaf = telex.ExceptionDetailsInfoList[^1];
        Assert.Equal(typeof(FileNotFoundException).FullName, leaf.TypeName);
        Assert.Contains("ROOT-CAUSE: the actual failure", leaf.Message, StringComparison.Ordinal);
        Assert.Contains("omitted", leaf.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetScrubbedStackFrames_KeepsMethodNames_RedactsFileNames() {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Exception ex = Thrown(new InvalidOperationException("boom"));

        var frames = TelemetryScrubber.GetScrubbedStackFrames(ex);

        Assert.NotEmpty(frames);
        // The throwing helper must still be identifiable by method name (don't destroy frames).
        Assert.Contains(frames, f => f.Method.Contains(nameof(Thrown), StringComparison.Ordinal));
        // Any source file names (present when PDBs are available) must not leak the profile path.
        Assert.All(frames, f => {
            if (f.FileName != null) {
                Assert.DoesNotContain(profile, f.FileName, StringComparison.OrdinalIgnoreCase);
            }
        });
    }
}
