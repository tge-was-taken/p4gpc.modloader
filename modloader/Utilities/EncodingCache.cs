using System;
using System.Collections.Generic;
using System.Text;

namespace modloader.Utilities
{
    public static class EncodingCache
    {
        public static readonly Encoding ShiftJIS;

        static EncodingCache()
        {
            Encoding.RegisterProvider( CodePagesEncodingProvider.Instance );
            ShiftJIS = Encoding.GetEncoding( 932 );
        }
    }
}
