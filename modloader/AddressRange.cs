namespace modloader
{
    /// <summary>
    /// Defines a physical address range with a minimum and maximum address.
    /// </summary>
    public struct AddressRange
    {
        public long Start;
        public long End;

        public AddressRange( long start, long end )
        {
            Start = start;
            End = end;
        }

        /// <summary>
        /// Returns true if the other address range is completely inside
        /// the current address range.
        /// </summary>
        public bool Contains(ref AddressRange otherRange)
        {
            if (otherRange.Start >= this.Start &&
                otherRange.End <= this.End)
                return true;

            return false;
        }

        /// <summary>
        /// Returns true if the other address range intersects another address range, i.e.
        /// start or end of this range falls inside other range.
        /// </summary>
        public bool Overlaps(ref AddressRange otherRange)
        {
            if (PointInRange(ref otherRange, this.Start))
                return true;

            if (PointInRange(ref otherRange, this.End))
                return true;

            if (PointInRange(ref this, otherRange.Start))
                return true;

            if (PointInRange(ref this, otherRange.End))
                return true;

            return false;
        }

        /// <summary>
        /// Returns true if a number "point", is between min and max of address range.
        /// </summary>
        private bool PointInRange(ref AddressRange range, long point)
        {
            if (point >= range.Start &&
                point <= range.End)
                return true;

            return false;
        }
    }
}
