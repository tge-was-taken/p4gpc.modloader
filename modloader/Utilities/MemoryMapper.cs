using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using Amicitia.IO;

namespace modloader.Utilities
{
    public class MemoryMapper
    {
        private class Block
        {
            public long Start;
            public long Size;
            public bool Used;
            public long End => Start + Size;

            public override string ToString()
            {
                return $"Block 0x{Start:X8} 0x{End:X8} (0x{Size:X8}) Used: {Used}";
            }
        }

        private LinkedList<Block> mBlocks;
        private readonly int mAlignment;

        public MemoryMapper( int alignment = 1 )
        {
            mBlocks = new LinkedList<Block>();
            mAlignment = alignment;
        }

        public long Allocate( long size )
        {
            // Find first free block thats big enough
            var block = mBlocks.First;
            while ( block != null )
                block = block.Next;

            if ( block == null )
            {
                // No free block was found
                // Add a new one
                DebugLog( $"Allocating new block 0x{size:X8}" );
                var end = AlignmentHelper.Align(mBlocks.Last.Value.End, mAlignment);
                if ( !Map( end, size, true ) )
                    return -1;

                // Return start of new block
                return end;
            }
            else
            {
                // Free block was found         
                if ( block.Value.Size > size )
                {
                    // Split the block if its too big
                    ResizeBlock( block, size );
                }

                // Return start of the block
                DebugLog( $"Reusing {block}" );
                return block.Value.Start;
            }
        }

        public long Reallocate( long start, long size )
        {
            DebugLog( $"Reallocating 0x{start:X8} 0x{size:X8}" );

            // Find block
            var block = mBlocks.First;
            while ( block != null && block.Value.Start != start )
                block = block.Next;

            if ( block == null )
            {
                return Allocate( size );
            }
            else
            {
                var newSize = ResizeBlock( block, size );
                if ( newSize == size ) 
                    return start;

                // Free & reallocate block
                Free( block );
                return Allocate( size );
            }
        }

        public bool Free( long start )
        {
            // Find block
            var block = mBlocks.First;
            while ( block != null && block.Value.Start != start )
                block = block.Next;

            return Free( block );
        }

        public bool Map( long start, long size, bool used )
        {
            // Try find last block before the start of the new block
            LinkedListNode<Block> prevBlock = null;
            var block = mBlocks.First;
            while ( block != null && block.Value.Start < start )
            {
                prevBlock = block;
                block = block.Next;        
            }

            if ( prevBlock != null )
            {
                // Check if the previous block overlaps with the new one
                var overlap = ( prevBlock.Value.Start + prevBlock.Value.Size ) - start;
                if ( overlap > 0 )
                {
                    if ( prevBlock.Value.Used )
                    {
                        // Previous block overlaps with the one we're trying to allocate
                        // so mapping isn't possible
                        return false;
                    }
                    else
                    {
                        // Reduce the size of the previous block so that it no longer overlaps
                        prevBlock.Value.Size -= overlap;
                    }
                }

                // Add the new block after the previous block
                mBlocks.AddAfter( prevBlock, new LinkedListNode<Block>( new Block() { Start = start, Size = size, Used = used } ) );
            }
            else
            {
                // Nothing is mapped before this address, so add a new block
                mBlocks.AddFirst( new LinkedListNode<Block>( new Block() { Start = start, Size = size, Used = used } ) );
            }

            return true;
        }

        private bool Free( LinkedListNode<Block> block )
        {
            DebugLog( $"Freeing {block.Value}" );

            if ( block == null )
            {
                // Block was never allocated
                return false;
            }
            else
            {
                // Free the block
                // Expand adjacent unused blocks if possible
                ResizeBlock( block, 0 );
            }

            return true;
        }

        private long ResizeBlock( LinkedListNode<Block> block, long newSize )
        {
            DebugLog( $"Resizing {block.Value}" );
            var offset = newSize - block.Value.Size;
            if ( offset == 0 ) return block.Value.Size;

            var canResize = true;
            if ( offset < 0 )
            {
                // If we're shrinking the block, expand the next block if it is not used
                if ( block.Next != null && !block.Next.Value.Used )
                {
                    // Expand the adjacent free block by moving it back
                    DebugLog( $"Moving (expanding) {block.Value} by {offset}" );
                    MoveBlock( block.Next, offset );
                }
                else
                {
                    // Add a new block                   
                    var newBlock = new Block() { Start = block.Value.Start + newSize, Size = offset * -1, Used = false };
                    DebugLog($"Adding {newBlock}" );
                    mBlocks.AddAfter( block, newBlock );
                }
            }
            else
            {
                // If we're expanding the block, shrink the next block if is not used
                if ( block.Next != null )
                {
                    if ( !block.Next.Value.Used )
                    {
                        // Shrink the adjacent free block by moving it forward
                        DebugLog( $"Moving (shrinking) {block.Value} by {offset}" );
                        MoveBlock( block.Next, offset );
                    }
                    else
                    {
                        // Can't resize the block
                        canResize = false;
                        DebugLog( $"Failed to resize {block.Value}" );
                    }
                }
                else
                {
                    // Block can be expanded to the right
                }
            }

            if ( canResize )
            {
                block.Value.Size += offset;

                if ( block.Value.Size == 0 )
                {
                    // Remove the block if its empty
                    DebugLog( $"Removing {block.Value}" );
                    mBlocks.Remove( block );
                }

                DebugLog( $"Resized {block.Value}" );
            }

            return block.Value.Size;
        }

        private void MoveBlock( LinkedListNode<Block> block, long offset )
        {
            block.Value.Start += offset;
            block.Value.Size -= offset;

            if ( block.Value.Size == 0 )
            {
                // Remove the block if its empty
                DebugLog( $"Removing {block.Value}" );
                mBlocks.Remove( block );
            }
        }

        [Conditional("DEBUG")]
        private void DebugLog( string msg )
            => Console.WriteLine( $"[memorymapper] D: {msg}" );
    }
}
