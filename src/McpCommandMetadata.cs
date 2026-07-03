// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Gui.Cli;
using IApplication = Terminal.Gui.App.IApplication;

namespace MCEControl;

/// <summary>
/// Registered with the Terminal.Gui.Cli host so <c>--help</c> and <c>--opencli</c> truthfully
/// describe the <c>mcp</c> mode. Never dispatched: <c>Program.Main</c> intercepts <c>mcp</c> /
/// <c>--mcp</c> before the CLI host, because the MCP server owns stdout as its JSON-RPC stream and
/// must run with no Terminal.Gui session around it.
/// </summary>
internal sealed class McpCommandMetadata : ICliCommand {
    public string PrimaryAlias => "mcp";
    public IReadOnlyList<string> Aliases => ["mcp"];
    public string Description =>
        "Run headless as an MCP stdio server (JSON-RPC on stdin/stdout; an MCP client spawns this). " +
        "Refused from the installed (Program Files) copy; see agent-guide.";
    public CommandKind Kind => CommandKind.Viewer;
    public Type ResultType => typeof(void);
    public IReadOnlyList<CommandOptionDescriptor> Options => [];

    // Fully qualified: MCEC has its own CommandResult (the agent result contract).
    public Task<Terminal.Gui.Cli.CommandResult> RunAsync(IApplication app, string? initial, CommandRunOptions options, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("'mcp' is dispatched by Program.Main before the CLI host; this command exists for metadata only.");
}
