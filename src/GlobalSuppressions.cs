// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

// Project-level analyzer suppressions. Only LIVE suppressions for rules that actually run under
// AnalysisLevel=latest-recommended (Directory.Build.props) belong here, each scoped to a specific
// target with a real justification. Do not re-add legacy FxCop-syntax or dead-target entries.
// The vendored src/WindowsInput fork is exempted wholesale in src/.editorconfig, not here.

using System.Diagnostics.CodeAnalysis;

// Win32 string-out P/Invokes (GetWindowText/GetClassName) use the standard StringBuilder marshalling;
// small buffers, not a hot path, so the char-buffer rewrite CA1838 wants isn't worth the interop risk.
[assembly: SuppressMessage("Interoperability", "CA1838:Avoid 'StringBuilder' parameters for P/Invokes", Scope = "type", Target = "~T:MCEControl.AgentNativeMethods", Justification = "Standard Win32 string-out marshalling; small buffers, not a hot path.")]
