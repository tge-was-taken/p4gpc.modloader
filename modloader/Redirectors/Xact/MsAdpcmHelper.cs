using System;
using System.Collections.Generic;
using System.Text;

namespace modloader.Redirectors.Xact
{
	public static class MsAdpcmHelper
	{
		public static long SamplesToByteOffset( long samples, int blockAlign, int channels )
		{
			int samplesPerBlock = blockAlign - 6 * channels;
			long start = samples / samplesPerBlock * blockAlign;
			long offset = samples % samplesPerBlock;
			return start + offset;
		}

		public static long BytesOffsetToSamples( long bytes, int blockAlign, int channels )
		{
			if ( blockAlign <= 0 || channels <= 0 )
				return 0L;

			return bytes / blockAlign * ( blockAlign - 6 * channels ) * 2 / channels + 
				( ( bytes % blockAlign != 0L ) ? ( ( bytes % blockAlign - 6 * channels ) * 2 / channels ) : 0 );
		}
	}
}
