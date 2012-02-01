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

namespace MCEControl
{
	/// <summary>
	/// Summary description for Util.
	/// </summary>
	public class Util
	{
		private Util()
		{
			//
			// TODO: Add constructor logic here
			//
		}

		public static void DumpException( Exception ex )
		{
			WriteExceptionInfo( ex );                    
			if( null != ex.InnerException )              
			{                                            
				WriteExceptionInfo( ex.InnerException ); 
			}

		}

		public static void WriteExceptionInfo( Exception ex )
		{
			Console.WriteLine( "--------- Exception Data ---------" );        
			Console.WriteLine( "Message: {0}", ex.Message );                  
			Console.WriteLine( "Exception Type: {0}", ex.GetType().FullName );
			Console.WriteLine( "Source: {0}", ex.Source );                    
			Console.WriteLine( "StrackTrace: {0}", ex.StackTrace );           
			Console.WriteLine( "TargetSite: {0}", ex.TargetSite );            
		}
	}


}
