using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

namespace MCEControl;
// Implements Command Pattern
public interface ICommand {
    /// <summary>
    /// Called to execute the command.
    /// </summary>
    bool Execute();

    Command Clone(Reply reply, Command clone);
}
