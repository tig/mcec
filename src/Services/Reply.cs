//-------------------------------------------------------------------
// Copyright © 2019 Kindel, LLC
// http://www.kindel.com
// charlie@kindel.com
//
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using log4net;

namespace MCEControl;

/// <summary>
/// The base class that each of MCE Controller's services are based
/// on (SocketServer, SocketClient, SerialServer).
///
/// Allows core code to be able to interact with services (e.g.
/// start, stop, configure, send replies) without having to
/// know what servic is active.
/// </summary>
public abstract class Reply {
    public abstract void Write(String text);
    public void WriteLine(String textLine) {
        Write(textLine + Environment.NewLine);
    }
}
