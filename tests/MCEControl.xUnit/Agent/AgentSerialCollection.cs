// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using Xunit;

namespace MCEControl.xUnit;

/// <summary>
/// Groups the agent test classes that mutate process-global state (<see cref="AgentRuntime.Settings"/>
/// and the telemetry singleton) into a single xUnit collection so they do NOT run in parallel with
/// one another. Each such test still sets and resets <c>AgentRuntime.Settings</c> itself; the
/// collection merely removes the cross-class race that parallel execution of a shared static creates.
/// </summary>
[CollectionDefinition("AgentSerial")]
public class AgentSerialCollection;
