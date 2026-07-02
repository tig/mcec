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
/// <remarks>
/// Accepted limitations (by design, to avoid over-redacting diagnostics): the username embedded
/// inside a longer hyphenated/munged segment (e.g. worktree/cache artifacts like
/// <c>C--Users-Tig-...</c>), DOS 8.3 short names for long usernames (e.g.
/// <c>C:\Users\LONGUS~1</c>), and prose mentions outside a path context (e.g.
/// <c>USERNAME=tig</c>) are not redacted.
/// </remarks>
public static class TelemetryScrubber {
    private const string UserProfileToken = "%USERPROFILE%";
    private const string UserNameToken = "%USERNAME%";

    // Cap on the number of exception details shipped. When a chain exceeds this, the outermost
    // details plus the deepest one (the root cause) are kept — see CreateScrubbedExceptionTelemetry.
    private const int MaxExceptionChainLength = 10;

    // Hard safety bound when walking a pathological exception graph (e.g. huge AggregateException
    // fan-out) so flattening always terminates quickly.
    private const int MaxFlattenedChainLength = 128;

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

        var chain = new List<(Exception Exception, int ParentIndex)>();
        FlattenExceptionChain(chain, ex, parentIndex: -1);

        // When the chain exceeds the cap, keep the outermost details plus the deepest one: the
        // leaf is the root cause and must survive truncation (PR #180 review). The kept leaf is
        // marked so it is obvious that intermediate wrappers were dropped.
        List<int> keptIndexes;
        int omitted = 0;
        if (chain.Count <= MaxExceptionChainLength) {
            keptIndexes = [.. Enumerable.Range(0, chain.Count)];
        }
        else {
            keptIndexes = [.. Enumerable.Range(0, MaxExceptionChainLength - 1), chain.Count - 1];
            omitted = chain.Count - MaxExceptionChainLength;
        }

        var idByIndex = new Dictionary<int, int>();
        for (int i = 0; i < keptIndexes.Count; i++) {
            idByIndex[keptIndexes[i]] = i + 1;
        }

        var details = new List<ExceptionDetailsInfo>(keptIndexes.Count);
        foreach (int index in keptIndexes) {
            (Exception e, int parentIndex) = chain[index];
            int id = idByIndex[index];
            // If this detail's parent was truncated away, chain it to the previous kept detail
            // so it stays attached to the tree.
            int outerId = parentIndex < 0 ? 0
                : idByIndex.TryGetValue(parentIndex, out int parentId) ? parentId : id - 1;
            string? messagePrefix = omitted > 0 && index == chain.Count - 1
                ? $"[{omitted} inner exception(s) omitted] "
                : null;
            details.Add(ToDetailsInfo(e, id, outerId, messagePrefix));
        }

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

    /// <summary>
    /// Depth-first flattening of the exception tree (following <see cref="AggregateException"/>
    /// inners and <see cref="Exception.InnerException"/>) into visitation order with parent links.
    /// </summary>
    private static void FlattenExceptionChain(List<(Exception Exception, int ParentIndex)> chain,
        Exception ex, int parentIndex) {
        if (chain.Count >= MaxFlattenedChainLength) {
            return;
        }

        int index = chain.Count;
        chain.Add((ex, parentIndex));

        if (ex is AggregateException aggregate) {
            foreach (Exception inner in aggregate.InnerExceptions) {
                FlattenExceptionChain(chain, inner, index);
            }
        }
        else if (ex.InnerException != null) {
            FlattenExceptionChain(chain, ex.InnerException, index);
        }
    }

    private static ExceptionDetailsInfo ToDetailsInfo(Exception ex, int id, int outerId, string? messagePrefix = null) {
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

        if (messagePrefix != null) {
            message = messagePrefix + message;
        }

        return new ExceptionDetailsInfo(id, outerId, ex.GetType().FullName ?? ex.GetType().Name, message,
            hasFullStack: true, ScrubUserPaths(ex.StackTrace), parsedStack);
    }
}
