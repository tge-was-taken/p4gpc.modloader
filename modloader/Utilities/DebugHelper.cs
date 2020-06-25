using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace modloader.Utilities
{
    public static class DebugHelper
    {
        [Conditional( "DEBUG" )]
        public static void LaunchDebugger()
            => Debugger.Launch();

        [Conditional("DEBUG")]
        public static void LaunchDebuggerIf( bool condition )
        {
            if ( condition )
                Debugger.Launch();
        }
    }
}
