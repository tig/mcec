//-------------------------------------------------------------------
// Copyright © 2019 Kindel, LLC
// http://www.kindel.com
// 
//
// Published under the MIT License.
// Source on GitHub: https://github.com/tig/mcec
//-------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;


namespace MCEControl;

/// <summary>
/// See https://stackoverflow.com/questions/5928976/what-is-the-proper-way-to-display-the-full-innerexception
/// </summary>
public static class ExtensionMethods {
    public static string FullMessage(this Exception ex) {
        if (ex is null) {
            throw new ArgumentNullException(nameof(ex));
        }

        if (ex is AggregateException aex) {
            return aex.InnerExceptions.Aggregate("[ ", (total, next) => $"{total}[{next.FullMessage()}] ") + "]";
        }

        string msg = ex.Message.Replace(", see inner exception.", "").Trim();
        string? innerMsg = ex.InnerException?.FullMessage();
        if (innerMsg is object && innerMsg != msg) {
            msg = $"{msg} \n[ {innerMsg} ]";
        }

        return msg;
    }
}
