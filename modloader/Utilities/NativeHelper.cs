using System;
using System.Collections.Generic;
using System.Text;

namespace modloader.Utilities
{
    public static unsafe class NativeHelper
    {
        public static int GetStringLength( byte* str )
        {
            int length = 0;
            while ( *str++ != 0 ) ++length;
            return length;
        }

        public static Span<byte> GetStringSpan( byte* str )
        {
            return new Span<byte>( ( void* )str, GetStringLength( str ) );
        }
    }
}
