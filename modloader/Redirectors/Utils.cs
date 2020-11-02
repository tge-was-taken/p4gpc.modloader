using System;
using System.Collections.Generic;
using System.Text;
using static modloader.Native;

namespace modloader.Redirectors
{
    public static unsafe class Utils
    {
        public static long ResolveReadFileOffset( long filePointer, LARGE_INTEGER* byteOffset )
        {
            var offset = filePointer;
            var reqOffset = ( byteOffset != null || ( byteOffset != null && byteOffset->HighPart == -1 && byteOffset->LowPart == FILE_USE_FILE_POINTER_POSITION ) ) ?
                byteOffset->QuadPart : -1;
            return reqOffset == -1 ? offset : reqOffset;
        }

        internal static object ResolveReadFileOffset( object filePointer, LARGE_INTEGER* byteOffset ) => throw new NotImplementedException();
    }
}
