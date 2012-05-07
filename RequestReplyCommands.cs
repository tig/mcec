//-------------------------------------------------------------------
// Copyright © 2012 Kindel Systems, LLC
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
    /// TODO: Determine how/if to implement commands that reply
    /// </summary>
    [Serializable]
    public class RequestReplyCommand : Command {
        public String Reply;

        public RequestReplyCommand() {
        }

        public RequestReplyCommand(String reply) {
            Reply = reply;
        }

        public override void Execute() {
        }
    }
}