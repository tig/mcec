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
public enum ServiceNotification {
    None = 0,
    Initialized = 1,
    StatusChange,
    ReceivedData,
    ClientConnected,
    ClientDisconnected,
    Write,
    WriteFailed,
    Error,
    Wakeup
}
