// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Runtime.CompilerServices;

// Expose internal members (e.g. AgentRuntime.SetEmergencyStopped) to the unit-test assembly so tests can
// drive process-global safety state directly without going through the input-injecting orchestration.
[assembly: InternalsVisibleTo("MCEControl.xUnit")]
