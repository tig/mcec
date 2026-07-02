// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System;
using System.Collections.Generic;

namespace MCEControl;

/// <summary>
/// One row of <see cref="CommandRegistry.Entries"/> (#204); everything the engine needs to know
/// about one <see cref="Command"/> type, stated explicitly in one place.
/// </summary>
/// <param name="XmlName">
/// The wire name of the command's element in .commands files (both as a top-level
/// <c>commandArray</c> item and as an <c>EmbeddedCommands</c> child). MUST be all-lowercase:
/// loading pipes every file through a lower-casing XSLT, so an uppercase name could be written but
/// never read back (#200). Changing an existing name breaks every installed .commands file.
/// </param>
/// <param name="CommandType">The concrete <see cref="Command"/> subclass serialized under <paramref name="XmlName"/>.</param>
/// <param name="BuiltIns">
/// Factory for the command's built-in prototypes (each with <c>Enabled=false</c> by default;
/// SECURITY). By convention this points at the type's <c>public static List&lt;Command&gt;
/// BuiltInCommands</c> property; referenced EXPLICITLY here, never discovered by reflection (the
/// old magic-static convention silently registered nothing when the property was misdeclared).
/// </param>
public sealed record CommandRegistryEntry(string XmlName, Type CommandType, Func<IEnumerable<Command>> BuiltIns);
