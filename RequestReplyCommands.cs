//-------------------------------------------------------------------
// By Charlie Kindel
// http://www.kindel.com
// charlie@kindel.com
// 
// Published under the BSD License.
// Source control on SourceForge 
//    http://sourceforge.net/projects/mcecontroller/
//-------------------------------------------------------------------

using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

namespace MCEControl 
{
	/// <summary>
	/// TODO: Determine how/if to implement commands that reply
	/// </summary>
	[Serializable()]
	public class RequestReplyCommand : Command
	{
		public String Reply;
		public RequestReplyCommand()
		{
		}
			
		public RequestReplyCommand(String Reply)
		{
			this.Reply = Reply;
		}

		public override void Execute()
		{

		}
	}
}
