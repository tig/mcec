// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.ApplicationInsights.DataContracts;
using AiStackFrame = Microsoft.ApplicationInsights.DataContracts.StackFrame;

namespace MCEControl;

/// <summary>
/// Redacts user-identifying filesystem paths from exception telemetry before it is shipped to
/// Application Insights (#156). Telemetry pseudonymizes the user (User.Id is a hash of
/// username/machine), but exception messages and stack traces routinely contain paths under
/// <c>C:\Users\&lt;username&gt;\AppData\...</c>, which would leak the cleartext Windows username
/// and defeat that anonymization. The scrubber replaces the user-profile directory with
/// <c>%USERPROFILE%</c> and the bare username, where it appears as a path segment, with
/// <c>%USERNAME%</c> — preserving the rest of the message, exception types, and method names.
/// </summary>
public static class TelemetryScrubber {
    private const string UserProfileToken = "%USERPROFILE%";
    private const string UserNameToken = "%USERNAME%";

    // Cap the inner-exception chain so a pathological exception graph can't bloat the payload.
    private const int MaxExceptionChainLength = 10;

    /// <summary>
    /// Scrubs user-identifying paths for the current environment
    /// (<see cref="Environment.SpecialFolder.UserProfile"/> and <see cref="Environment.UserName"/>).
    /// </summary>
    public static string ScrubUserPaths(string? text) =>
        ScrubUserPaths(text, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.UserName);

    /// <summary>
    /// Pure overload for testability: replaces <paramref name="userProfilePath"/> with
    /// <c>%USERPROFILE%</c> and <paramref name="userName"/>, where it appears as a path segment,
    /// with <c>%USERNAME%</c>. Matching is case-insensitive (Windows paths) and accepts both
    /// <c>\</c> and <c>/</c> separators. Returns <see cref="string.Empty"/> for null/empty input.
    /// </summary>
    public static string ScrubUserPaths(string? text, string? userProfilePath, string? userName) {
        if (string.IsNullOrEmpty(text)) {
            return string.Empty;
        }

        string result = text;

        if (!string.IsNullOrEmpty(userProfilePath)) {
            // Match the profile dir with either separator style. Require a segment boundary after
            // it so C:\Users\tig does not match inside C:\Users\tigger.
            string profilePattern = Regex.Escape(userProfilePath).Replace(@"\\", @"[\\/]") + @"(?![\w\-])";
            result = Regex.Replace(result, profilePattern, UserProfileToken,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        if (!string.IsNullOrEmpty(userName)) {
            // The bare username as a path segment: either preceded by a separator and ending at a
            // segment boundary, or starting a relative path that continues with a separator.
            // Word-ish characters ([\w-]) on the boundary mean it is part of a longer name
            // (e.g. "tigger") and must not be redacted.
            string escaped = Regex.Escape(userName);
            string namePattern = $@"(?<=[\\/]){escaped}(?=$|[^\w\-])|(?<=^|[^\w\-]){escaped}(?=[\\/])";
            result = Regex.Replace(result, namePattern, UserNameToken,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return result;
    }

    /// <summary>
    /// Builds an <see cref="ExceptionTelemetry"/> for <paramref name="ex"/> (including its inner
    /// exceptions) whose messages, stack strings, and stack-frame file names have been scrubbed
    /// with <see cref="ScrubUserPaths(string?)"/>. Exception types, method names, and line numbers
    /// are preserved so the telemetry stays diagnosable.
    /// </summary>
    public static ExceptionTelemetry CreateScrubbedExceptionTelemetry(Exception ex) {
        ArgumentNullException.ThrowIfNull(ex);

        var details = new List<ExceptionDetailsInfo>();
        AddExceptionDetails(details, ex, outerId: 0);

        var frames = GetScrubbedStackFrames(ex);
        string topMethod = frames.Count > 0 ? frames[0].Method : "<unknown>";
        string problemId = $"{ex.GetType().FullName} at {topMethod}";

        return new ExceptionTelemetry(details, severityLevel: null, problemId,
            new Dictionary<string, string>(), new Dictionary<string, double>());
    }

    /// <summary>
    /// Extracts the stack frames of <paramref name="ex"/> with any source file names scrubbed.
    /// Internal so tests can verify frames survive scrubbing (method names and line numbers are
    /// kept; only file paths are redacted).
    /// </summary>
    internal static IReadOnlyList<(string Method, string Assembly, string? FileName, int Line)> GetScrubbedStackFrames(Exception ex) {
        var result = new List<(string Method, string Assembly, string? FileName, int Line)>();
        foreach (System.Diagnostics.StackFrame frame in new StackTrace(ex, fNeedFileInfo: true).GetFrames()) {
            MethodBase? method = frame.GetMethod();
            string methodName = method == null
                ? "<unknown>"
                : method.DeclaringType == null ? method.Name : $"{method.DeclaringType.FullName}.{method.Name}";
            string assembly = method?.Module.Assembly.FullName ?? string.Empty;
            string? fileName = frame.GetFileName();
            if (fileName != null) {
                fileName = ScrubUserPaths(fileName);
            }

            result.Add((methodName, assembly, fileName, frame.GetFileLineNumber()));
        }

        return result;
    }

    private static void AddExceptionDetails(List<ExceptionDetailsInfo> details, Exception ex, int outerId) {
        if (details.Count >= MaxExceptionChainLength) {
            return;
        }

        int id = details.Count + 1;
        details.Add(ToDetailsInfo(ex, id, outerId));

        if (ex is AggregateException aggregate) {
            foreach (Exception inner in aggregate.InnerExceptions) {
                AddExceptionDetails(details, inner, id);
            }
        }
        else if (ex.InnerException != null) {
            AddExceptionDetails(details, ex.InnerException, id);
        }
    }

    private static ExceptionDetailsInfo ToDetailsInfo(Exception ex, int id, int outerId) {
        var frames = GetScrubbedStackFrames(ex);
        var parsedStack = new List<AiStackFrame>(frames.Count);
        for (int level = 0; level < frames.Count; level++) {
            (string method, string assembly, string? fileName, int line) = frames[level];
            parsedStack.Add(new AiStackFrame(assembly, fileName, level, line, method));
        }

        string message = ScrubUserPaths(ex.Message);
        if (string.IsNullOrWhiteSpace(message)) {
            message = "n/a";
        }

        return new ExceptionDetailsInfo(id, outerId, ex.GetType().FullName ?? ex.GetType().Name, message,
            hasFullStack: true, ScrubUserPaths(ex.StackTrace), parsedStack);
    }
}
