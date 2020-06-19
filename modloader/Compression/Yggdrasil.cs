// Ported & adapted from https://github.com/agentOfChaos/oversized_syringe/blob/master/ovsylib/compresion_algos/yggdrasil.py
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace modloader.Compression
{
    /*
    compression algorithm based on the following structure:
    Note: the data structures aren't aligned at all, meaning we'll have to parse it bit by bit.
    ([algo1 header] actually handled elsewhere, this code deals with data starting in the next area)
    [vectorized tree]
    [compressed data]
    Basically, it employs Huffman coding. I called it Yggdrasil because I didn't know about HC yet,
    but I knew data structures.
    vectorialized tree structure:
    A sequence of units looks like this:
    [spacer][payload byte]
    I like to call it 'spacer' because of historical reasons, since for some of the re-eng time those
    little fellas where a complete mistery.
    The spacer is a sequence of bit like this: 1*0 (* is the kleene star, duh)
    The payload byte is an actual data byte, in uncompressed form
    The tree is built like this:
    1) Initialize the tree with a root node
    2) The root node is the leftmost active node
    3) Repeat until the tree is fully populated:
        a) Select the leftmost active node (the one which is not a leaf, but has no children yet)
        b) The number of '1's in the spacer tells us how many expansions to do (add 2 non-leaf children to the active node);
           the drill is: expand one, the leftmost becomes the active node, repeat until all the expansions are done
        c) If the expansions are done (0 expansions is possible), turn the current active node into a leaf,
           its value is specified in the 8 bits following the spacer
    After the tree is built, the compressed data starts right away
    A unit of data looks like this:
    [one or more 'navigator bits']
    The data is parsed like this:
    Repeat until length(written data) == uncompressed value specified in header
        1) current working node (I like to call it 'tarzan') := tree's root
        2) repeat while the working node is not a leaf node:
            a) read one navigator bit
            b) if the bit is 0, then update working node := current working node's left child
            c) if the bit is 1, then update working node := current working node's right child
        3) write out the current working node's value (a byte)
        4) increment written_data , repeat
    */
    public static class Yggdrasil
    {
        public static TreeNode tempRoot;

        public class TreeNode
        {
            public TreeNode childzero;
            public TreeNode childone;
            public bool isleaf;
            public bool isActive;
            public byte value;

            public TreeNode()
            {
                isActive = true;
            }

            public void set_value( byte value )
            {
                isActive = false;
                isleaf = true;
                this.value = value;
            }

            public TreeNode expand_node()
            {
                return bilateral_expand()[0];
            }

            public TreeNode[] bilateral_expand()
            {
                isActive = false;
                isleaf = false;
                childzero = new TreeNode();
                childone = new TreeNode();
                return new[] { childzero, childone };
            }

            public TreeNode find_first_active()
            {
                if ( isleaf )
                    return null;

                if ( isActive )
                    return this;

                if ( childzero.isActive )
                    return childzero;

                var deepLeft = childzero.find_first_active();
                if ( deepLeft != null )
                    return deepLeft;

                if ( childone.isActive )
                    return childone;

                var deepRight = childone.find_first_active();
                if ( deepRight != null )
                    return deepRight;

                return null;
            }
        }

        public static void tree2dot(TreeNode root, string filename)
        {
            var seenNodes = new List<TreeNode>();

            void Traversal(TreeNode node)
            {
                seenNodes.Add( node );
                if (!node.isleaf)
                {
                    Traversal( node.childzero );
                    Traversal( node.childone );
                }    
            }

            Traversal( root );
            using ( var tfile = File.CreateText( filename ) )
            {
                tfile.WriteLine( "digraph yggdraasil {" );
                foreach ( var node in seenNodes )
                {
                    tfile.Write( "node_" + seenNodes.IndexOf( node ) );
                    if ( node.isleaf )
                        tfile.Write( $" [label=\"{node.value:X}\"]" );
                    else
                        tfile.Write( " [label=\"\"" );

                    tfile.WriteLine();
                }

                foreach ( var node in seenNodes )
                {
                    if ( !node.isleaf )
                    {
                        tfile.WriteLine( $"node_{seenNodes.IndexOf( node )} -> node_{seenNodes.IndexOf( node.childzero )}" +
                            $" [label=0]" );
                        tfile.WriteLine( $"node_{seenNodes.IndexOf( node )} -> node_{seenNodes.IndexOf( node.childone )}" +
                            $" [label=1]" );
                    }
                }
            }
        }

        public static (TreeNode root, int cursor) buildtree( TreeNode root, int cursor, BitStream bitstream )
        {
            while ( true )
            {
                // get the active node to work on
                var worknode = root.find_first_active();
                if ( worknode == null ) // if the tree is completely built, then stop
                    break;
                // read the 'spacers'
                var downleft_distance = 0;
                try
                {
                    while ( bitstream[cursor] )
                    {
                        downleft_distance += 1;
                        cursor += 1;
                    }
                }
                catch ( IndexOutOfRangeException )
                {
                    Console.WriteLine( $"Tree parsing aborted: cursor at HEADER + {( int )Math.Floor( ( double )cursor / 8 ):X}, bit #{cursor % 8}" );
                    return (null, 0);
                }

                cursor += 1;
                // read the byte
                var value = bitstream.ReadByte(cursor, cursor+8);
                cursor += 8;
                for ( int i = 0; i < downleft_distance; i++ )
                {
                    worknode = worknode.expand_node();
                }

                worknode.set_value( value );
            }

            return (root, cursor);
        }

        public static void DecompressBlock( Stream binfile, Stream destfile, int numbytes, int bytes_out, bool debuggy = false )
        {
            var bytes = new byte[numbytes];
            binfile.Read( bytes );
            var bitstream = new BitStream( Endianness.Big );
            bitstream.FromBytes( bytes );
            var cursor = 0;
            var root = new TreeNode();
            var bytes_written = 0;

            (root, cursor) = buildtree( root, cursor, bitstream );
            tempRoot = root;
            if ( root == null )
            {
                Console.WriteLine( "Tree construction failed, exiting" );
                return;
            }

            if ( debuggy )
            {
                Console.WriteLine( $"Tree parsing finished: cursor at {( int )Math.Floor( ( double )( cursor / 8 ) ):X}, bit #{cursor % 8}" );
                tree2dot( root, "debugtree.dot" );
            }

            while ( bytes_written < bytes_out )
            {
                var tarzan = root;
                while ( !tarzan.isleaf )
                {
                    bool chu;
                    try
                    {
                        chu = bitstream[cursor];
                    }
                    catch ( IndexOutOfRangeException )
                    {
                        Console.WriteLine( $"Data parsing aborted: end of bitstream, cursor at {Math.Floor( ( double )( cursor / 8 ) ):X}, bit #{cursor % 8} ({bytes_written}/{bytes_out} bytes written)" );
                        return;
                    }
                    cursor += 1;
                    if ( !chu )
                    {
                        tarzan = tarzan.childzero;
                    }
                    else
                    {
                        tarzan = tarzan.childone;
                    }
                }

                destfile.WriteByte( tarzan.value );
                bytes_written += 1;
            }
        }

        public static void Decompress( Stream binfile, Stream destfile )
        {
            // Compressed file header
            using ( var reader = new BinaryReader( binfile, Encoding.Default, true ) )
            {
                var magic = reader.ReadInt32();
                var chunkCount = reader.ReadInt32();
                var chunkSize = reader.ReadInt32();
                var headerSize = reader.ReadInt32();
                for ( int i = 0; i < chunkCount; i++ )
                {
                    var chunkUncompressedSize = reader.ReadInt32();
                    var chunkCompressedSize = reader.ReadInt32();
                    var dataOffset = reader.ReadInt32();
                    var next = reader.BaseStream.Position;
                    reader.BaseStream.Seek( headerSize + dataOffset, SeekOrigin.Begin );
                    DecompressBlock( reader.BaseStream, destfile, chunkCompressedSize, chunkUncompressedSize, true );
                    reader.BaseStream.Seek( next, SeekOrigin.Begin );
                    return;
                }
            }
        }

        public static void collectBytes( List<byte> Lb, Dictionary<byte, int> multib, Stream sourcefile, int start_offs, int end_offs )
        {
            sourcefile.Seek( start_offs, 0 );
            var readbyte = sourcefile.ReadByte();
            var target_read = 0;
            while ( readbyte != -1 && ( target_read < ( end_offs - start_offs ) ) )
            {
                if ( !Lb.Contains( (byte)readbyte ) )
                {
                    Lb.Add( (byte)readbyte );
                    multib[(byte)readbyte] = 1;
                }
                else
                {
                    multib[(byte)readbyte] += 1;
                }
                readbyte = sourcefile.ReadByte();
                target_read += 1;
            }
        }

        public class LbComparer : IComparer<byte>
        {
            private readonly Dictionary<byte, int> mMultib;

            public LbComparer(Dictionary<byte, int> multib)
            {
                mMultib = multib;
            }

            public int Compare( [AllowNull] byte x, [AllowNull] byte y )
            {
                return mMultib[x].CompareTo( mMultib[y] );
            }
        }

        public static TreeNode buildHuffmanTree( Stream sourcefile, int start_offs, int end_offs )
        {
            var Lb = new List<byte>();
            var multib = new Dictionary<byte, int>();
            var nodemap = new List<(byte, TreeNode, int)>();
            collectBytes( Lb, multib, sourcefile, start_offs, end_offs );
            //Lb.Sort( new LbComparer( multib ) );
            Lb = Lb.OrderBy( x => multib[x] ).ToList();

            foreach ( var b in Lb )
            {
                var nn = new TreeNode();
                nn.set_value( b );
                nodemap.Add( (b, nn, multib[b]) );
            }

            while ( nodemap.Count > 1 )
            {
                // TODO: verify pop
                var uno = nodemap[nodemap.Count - 1];
                nodemap.Remove( uno );
                var due = nodemap[nodemap.Count - 1];
                nodemap.Remove( due );
                var radix = new TreeNode();
                radix.childzero = uno.Item2;
                radix.childone = due.Item2;
                var newcost = uno.Item3 + due.Item3;
                var i = 0;
                while ( i < nodemap.Count )
                {
                    if ( nodemap[i].Item3 > newcost )
                        break;
                    i += 1;
                }
                nodemap.Insert( i, (0, radix, newcost) );
            }

            return nodemap[0].Item2;
        }

        public static BitStream CompressBlock( Stream sourcefile, int start_offs, int end_offs, bool debuggy = false )
        {
            // :return: bitarray object containing the compressed data
            var lookup_table = new Dictionary<byte, BitStream>();
            var vecbuild_path = new BitStream( Endianness.Big ); // TODO verify endian
            var out_bitstream = new BitStream( Endianness.Big ); // TODO verify endian

            void build_lookup_table( TreeNode node, BitStream path, byte search )
            {
                if ( !node.isleaf )
                {
                    var lefty = path.Copy();
                    lefty.WriteBit( false );
                    var righty = path.Copy();
                    righty.WriteBit( true );
                    build_lookup_table( node.childzero, lefty, search );
                    build_lookup_table( node.childone, righty, search );
                }
                else if ( node.value == search )
                {
                    lookup_table[search] = path;
                }
            }

            void build_vector_tree( TreeNode node )
            {
                if ( node.isleaf )
                {
                    vecbuild_path.WriteBit( false );
                    vecbuild_path.Position = 0;
                    vecbuild_path.CopyTo( out_bitstream );
                    vecbuild_path = new BitStream( Endianness.Big );
                    out_bitstream.WriteByte( node.value );
                }
                else
                {
                    vecbuild_path.WriteBit( true );
                    build_vector_tree( node.childzero );
                    build_vector_tree( node.childone );
                }
            }

            //var tree = buildHuffmanTree(sourcefile, start_offs, end_offs);
            var tree = tempRoot;
            sourcefile.Seek( start_offs, SeekOrigin.Begin ); // verify seek

            if ( debuggy )
                tree2dot( tree, "optimumtree.dot" );

            build_vector_tree( tree );

            var datum = sourcefile.ReadByte();
            var target_read = 0;
            while ( datum != -1 && ( target_read < ( end_offs - start_offs ) ) )
            {
                if ( !lookup_table.ContainsKey( ( byte )datum ) )
                {
                    build_lookup_table( tree, new BitStream( Endianness.Big ), ( byte )datum );
                }
                lookup_table[( byte )datum].CopyTo( out_bitstream );
                datum = sourcefile.ReadByte();
                target_read += 1;
            }

            return out_bitstream;
        }

        //public static void compress( Stream sourcefile, Stream destfile, int start_offs, int end_offs, bool debuggy = false )
        //{
        //    // :return: bitarray object containing the compressed data
        //    var lookup_table = new Dictionary<byte, List<bool>>();
        //    var vecbuild_path = new List<bool>(); // TODO verify endian

        //    void build_lookup_table( TreeNode node, List<bool> path, byte search )
        //    {
        //        if ( !node.isleaf )
        //        {
        //            var lefty = new List<bool>(path);
        //            lefty.Add( false );
        //            var righty = new List<bool>(path);
        //            righty.Add( true );
        //            build_lookup_table( node.childzero, lefty, search );
        //            build_lookup_table( node.childone, righty, search );
        //        }
        //        else if ( node.value == search )
        //        {
        //            lookup_table[search] = path;
        //        }
        //    }

        //    void build_vector_tree( TreeNode node )
        //    {
        //        if ( node.isleaf )
        //        {
        //            vecbuild_path.Add( false );
        //            WriteBits( destfile, vecbuild_path );
        //            vecbuild_path.Clear();
        //            destfile.WriteByte( node.value );
        //        }
        //        else
        //        {
        //            vecbuild_path.Add( true );
        //            build_vector_tree( node.childzero );
        //            build_vector_tree( node.childone );
        //        }
        //    }

        //    var tree = buildHuffmanTree(sourcefile, start_offs, end_offs);
        //    sourcefile.Seek( start_offs, SeekOrigin.Begin ); // verify seek

        //    if ( debuggy )
        //        tree2dot( tree, "optimumtree.dot" );

        //    build_vector_tree( tree );

        //    var datum = sourcefile.ReadByte();
        //    var target_read = 0;
        //    while ( datum != -1 && ( target_read < ( end_offs - start_offs ) ) )
        //    {
        //        if ( !lookup_table.ContainsKey( ( byte )datum ) )
        //        {
        //            build_lookup_table( tree, new List<bool>(), ( byte )datum );
        //        }
        //        WriteBits( destfile, lookup_table[( byte )datum] );
        //        datum = sourcefile.ReadByte();
        //        target_read += 1;
        //    }
        //}
    }

    public enum Endianness
    {
        Little,
        Big
    }

    public class BitStream : Stream
    {
        private int mBitIndex;
        private byte mBits;
        private Stream mStream;

        public Endianness Endianness { get; }
        public override bool CanRead => mStream.CanRead;
        public override bool CanSeek => mStream.CanSeek;
        public override bool CanWrite => mStream.CanWrite;
        public override long Length => mStream.Length;
        public override long Position
        {
            get => mStream.Position;
            set => Seek( value, SeekOrigin.Begin );
        }

        public bool this[int i]
        {
            get => ReadBit( i );
            //set => WriteBit( i, value );
        }

        public BitStream( Endianness endianness )
        {
            Endianness = endianness;
            mBitIndex = -1;
            mStream = new MemoryStream();
        }

        public void FromBytes( byte[] bytes )
        {
            mStream = new MemoryStream( bytes );
        }

        public void FromBytes( List<byte> bytes )
        {
            mStream = new MemoryStream( bytes.Count );
            for ( int i = 0; i < bytes.Count; i++ )
                mStream.WriteByte( bytes[i] );
        }

        public void FromStream( Stream stream )
        {
            mStream = stream;
        }

        public byte ReadByte( int from, int to )
        {
            var count = to - from;
            byte b = 0;

            for ( int bitIndex = 0; bitIndex < count; bitIndex++ )
            {
                if ( this[from + bitIndex] )
                    b |= ( byte )( 1 << ( 7 - bitIndex ) );
            }

            return b;
        }

        public bool ReadBit( int index )
        {
            return GetBit( ReadByteAtOffset(index / 8), index % 8 );
        }

        public bool ReadBit()
        {
            if ( mBitIndex < 0 )
            {
                mBits = PeekByte();
                mBitIndex = 0;
            }
            else if ( mBitIndex > 7 )
            {
                var temp = mStream.ReadByte();
                if ( temp == -1 ) throw new IOException();
                mBits = ( byte )temp;
            }

            return GetBit( mBits, mBitIndex++ );
        }

        //public void WriteBit( int index, bool value )
        //{
        //    FlushCache();
        //    InternalWriteBit( ReadByteAtOffset( index / 8 ), index, value );
        //}

        public void WriteBit( bool value )
        {
            if ( mBitIndex < 0 )
            {
                mBits = PeekByte();
                mBitIndex = 0;
            }
            else if ( mBitIndex > 7 )
            {
                mStream.WriteByte( mBits );
                mBits = PeekByte();
                mBitIndex = 0;
            }

            mBits = SetBit( mBits, mBitIndex++, value );
        }

        public override void WriteByte( byte value )
        {
            // TODO verify order
            for ( int i = 0; i < 8; i++ )
                WriteBit( ( value & ( 1 << i ) ) != 0 );
        }

        public void CopyTo( BitStream other )
        {
            while ( Position < Length )
                other.WriteBit( ReadBit() );
        }

        private void FlushCache()
        {
            if ( mBitIndex > 0 )
            {
                mStream.WriteByte( mBits );
                mStream.Position--;
            }

            mBitIndex = -1;
        }

        public override void Flush()
        {
            FlushCache();
            mStream.Flush();
        }

        public override int Read( byte[] buffer, int offset, int count )
        {
            FlushCache();
            return mStream.Read( buffer, offset, count );
        }

        public override long Seek( long offset, SeekOrigin origin )
        {
            var position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => Position + offset,
                SeekOrigin.End => Length - offset,
            };

            if ( position != Position )
                FlushCache();
            return mStream.Seek( offset, origin );
        }

        public override void SetLength( long value )
        {
            mStream.SetLength( value );
        }

        public override void Write( byte[] buffer, int offset, int count )
        {
            for ( int i = 0; i < count; i++ )
                WriteByte( buffer[i + offset] );
        }

        public BitStream Copy()
        {
            var pos = Position;
            var copy = new BitStream( Endianness );
            CopyTo( copy );
            Position = pos;
            return copy;
        }

        private bool GetBit( byte value, int index )
        {
            var realBitIndex = Endianness == Endianness.Little ? index : 7 - index;
            return ( value & ( 1 << realBitIndex ) ) != 0;
        }

        private byte SetBit( byte bits, int index, bool value )
        {
            var realBitIndex = Endianness == Endianness.Little ? index : 7 - index;
            return ( byte )( ( bits & ~( 1 << realBitIndex ) ) | Unsafe.As<bool, byte>( ref value ) << realBitIndex );
        }

        private byte ReadByteAtOffset( long offset )
        {
            var temp = Position;
            mStream.Position = offset;
            var value = ( byte )mStream.ReadByte();
            mStream.Position = temp;
            return value;
        }

        private void WriteByteAtOffset( long offset, byte value )
        {
            var temp = Position;
            mStream.Position = offset;
            mStream.WriteByte( value );
            mStream.Position = temp;
        }

        private byte PeekByte()
        {
            var temp = mStream.ReadByte();
            if ( temp == -1 )
            {
                return 0;
            }
            else
            {
                Position -= 1;
                return ( byte )temp;
            }
        }
    }
}
