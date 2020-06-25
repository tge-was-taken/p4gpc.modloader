using System;
using System.Collections.Generic;
using System.Text;
using Amicitia.IO;
using modloader.Formats.Xact;

namespace modloader.Redirectors.Xact
{
    public class WaveBankFormatHelper
    {
        public static bool TryGetSamplesToByteOffset( WaveBankMiniFormatTag format, WaveBankMiniFormatBitDepth bitDepth, int samples, int blockAlign, int channels, out int byteOffset )
        {
            switch ( format )
            {
                case WaveBankMiniFormatTag.PCM:
                    byteOffset = bitDepth == WaveBankMiniFormatBitDepth._8 ?
                        ( samples * ( 8 /* bits */ / 8 ) ) * channels :
                        ( samples * ( 16 /* bits */ / 8 ) ) * channels;
                    return true;
                                        
                case WaveBankMiniFormatTag.XMA:
                    byteOffset = ( samples * ( 16 /* bits */ / 8 ) ) * channels;
                    return true;

                case WaveBankMiniFormatTag.ADPCM:
                    byteOffset = ( int )MsAdpcmHelper.SamplesToByteOffset( samples, blockAlign, channels );
                    return true;

                case WaveBankMiniFormatTag.WMA:
                    byteOffset = ( samples * ( 16 /* bits */ / 8 ) ) * channels;
                    return true;
            }

            byteOffset = 0;
            return false;
        }

        public static int AlignSamples( WaveBankMiniFormatTag format, int samples, int blockAlign, int channels )
        {
            switch ( format )
            {
                case WaveBankMiniFormatTag.ADPCM:
                    //return samples;
                    return AlignmentHelper.Align( samples, blockAlign - ( 6 * channels ) );
                default:
                    return AlignmentHelper.Align( samples, blockAlign );
            }
        }
    }
}
