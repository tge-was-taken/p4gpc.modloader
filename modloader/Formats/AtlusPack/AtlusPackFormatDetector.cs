using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Amicitia.IO.Binary;

namespace modloader.Formats.AtlusPAK
{
    public enum AtlusPackFormat
    {
        Unknown,

        /// <summary>
        /// Entry: 252 bytes filename, 4 bytes filesize
        /// </summary>
        V1,

        /// <summary>
        /// Header: 4 bytes entry count Entry: 32 bytes filename, 4 bytes filesize
        /// </summary>
        V2,

        /// <summary>
        /// Header: 4 bytes entry count Entry: 24 bytes filename, 4 bytes filesize
        /// </summary>
        V3
    }

    public unsafe static class AtlusPackFormatDetector
    {
        private const int MAX_LENGTH_SANITY_VALUE = ( 1024 * 1024 * 1024 );

        public static (AtlusPackFormat, Endianness) DetectFormat( ReadOnlySpan<byte> buffer )
        {
            if ( IsFormatV1( buffer, out var endianness ) )
                return (AtlusPackFormat.V1, endianness);

            if ( IsFormatV2OrV3( buffer, 36, out endianness ) )
                return (AtlusPackFormat.V2, endianness);

            if ( IsFormatV2OrV3( buffer, 28, out endianness ) )
                return (AtlusPackFormat.V3, endianness);

            return (AtlusPackFormat.Unknown, endianness);
        }

        private static bool IsFormatV1( ReadOnlySpan<byte> buffer, out Endianness endianness )
        {
            endianness = Endianness.Little;

            // Need at least 256 bytes
            if ( buffer.Length < 256 )
                return false;

            if ( buffer[0] == 0 )
            {
                // First byte is zero.
                // V1 not an option, as it would make for an invalid entry name
                // V2-3 are not an option either, as it would mean the entry count is 0
                // Either invalid, or big endian
                return false;
            }

            bool nameTerminated = false;
            for ( int i = 0; i < 252; i++ )
            {
                if ( buffer[i] == 0 ) nameTerminated = true;

                // If the name has already been terminated but there's still data in the reserved space,
                // fail the test
                if ( nameTerminated && buffer[i] != 0 )
                    return false;
            }

            var length = BinaryPrimitives.ReadInt32LittleEndian( buffer.Slice( 252 ) );
            if ( length < 0 || length >= MAX_LENGTH_SANITY_VALUE )
            {
                BinaryOperations<int>.Reverse( ref length );
                if ( length < 0 || length >= MAX_LENGTH_SANITY_VALUE )
                    return false;

                endianness = Endianness.Big;
            }

            return true;
        }

        private static bool IsFormatV2OrV3( ReadOnlySpan<byte> buffer, int entrySize, out Endianness endianness )
        {
            endianness = Endianness.Little;

            // Need at least a full entry
            if ( buffer.Length < 4 + entrySize )
                return false;

            var entryCount = BinaryPrimitives.ReadInt32LittleEndian(buffer);
            if ( entryCount <= 0 || entryCount > 1024 )
            {
                BinaryOperations<int>.Reverse( ref entryCount );
                if ( entryCount <= 0 || entryCount > 1024 )
                    return false;

                endianness = Endianness.Big;
            }

            // check if the name field is correct
            if ( buffer[4] == 0 )
                return false;

            var nameTerminated = false;
            for ( int i = 0; i < ( entrySize - 4 ); i++ )
            {
                if ( buffer[4 + i] == 0x00 )
                {
                    nameTerminated = true;
                }
                else if ( nameTerminated )
                {
                    return false;
                }
            }

            // Data length sanity check
            var length = BinaryPrimitives.ReadInt32LittleEndian( buffer.Slice( 4 + entrySize - 4 ) );
            if ( length < 0 || length >= MAX_LENGTH_SANITY_VALUE )
            {
                BinaryOperations<int>.Reverse( ref length );
                if ( length < 0 || length >= MAX_LENGTH_SANITY_VALUE )
                    return false;

                endianness = Endianness.Big;
            }

            return true;
        }
    }
}
