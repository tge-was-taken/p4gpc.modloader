using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace modloader.Utilities
{
    public static unsafe class SpanHelper
    {
        public static Span<T2> AsSpan<T1, T2>( ref T1 val ) 
            where T1 : unmanaged
            where T2 : unmanaged
        {
            Span<T1> valSpan = MemoryMarshal.CreateSpan(ref val, 1);
            return MemoryMarshal.Cast<T1, T2>( valSpan );
        }
    }

    public static unsafe class NativeStringHelper
    {
        public static int GetLength( byte* str )
        {
            int length = 0;
            while ( *str++ != 0 ) ++length;
            return length;
        }

        public static Span<byte> AsSpan( byte* str )
        {
            return new Span<byte>( ( void* )str, GetLength( str ) );
        }
    }
}
