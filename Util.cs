//-------------------------------------------------------------------
// Copyright © 2019 Kindel Systems, LLC
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the MIT License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------

using System;

namespace MCEControl {
    /// <summary>
    /// Summary description for Util.
    /// </summary>
    public class Util {
        private Util() {
            //
            // TODO: Add constructor logic here
            //
        }

        public static void DumpException(Exception ex) {
            WriteExceptionInfo(ex);
            if (null != ex.InnerException) {
                WriteExceptionInfo(ex.InnerException);
            }
        }

        public static void WriteExceptionInfo(Exception ex) {
            Logger.Instance.Log4.Debug($"--------- Exception Data ---------");
            Logger.Instance.Log4.Debug($"Message: {ex.Message}");
            Logger.Instance.Log4.Debug($"Exception Type: {ex.GetType().FullName}");
            Logger.Instance.Log4.Debug($"Source: {ex.Source}");
            Logger.Instance.Log4.Debug($"StrackTrace: {ex.StackTrace}");
            Logger.Instance.Log4.Debug($"TargetSite: {ex.TargetSite}");
        }
    }
}
