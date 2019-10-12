using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Go
{
    /// <summary>
    /// Represents a group of stones (or empty spaces) on a board object. This
    /// object is context-free, i.e. it is not associated with a specific board.
    /// In essence it is simply a set of board coordinates, with an associated
    /// content (black, white or empty), and state (dead or alive for scoring
    /// purposes).
    /// </summary>
    public sealed class Group
    {
        public static int SizeX;
        public static int SizeY;

        private uint pointsMask;
        public uint PointsMask { get { return pointsMask; } }
        private uint neighboursMask;

        /// <remarks>
        /// Copied from:
        /// <a href="https://stackoverflow.com/a/12171691/1417849">
        /// Aug 29 '12 at 5:56 Jon Skeet
        /// </a>
        public static int CountBits(uint value)
        {
            int count = 0;
            while (value != 0)
            {
                count++;
                value &= value - 1;
            }
            return count;
        }

        public int NumPoints()
        {
            return CountBits(pointsMask);
        }

        public int NumNeighbours()
        {
            return CountBits(neighboursMask);
        }

        public bool AnyPointsIntersect(List<Group> groups)
        {
            foreach (Group group in groups)
            {
                if ((pointsMask & group.pointsMask) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the content of the group.
        /// </summary>
        public Content Content { get; set; }

        /// <summary>
        /// Gets or sets whether this group is dead for the purposes of scoring.
        /// </summary>
        public bool IsDead { get; set; }

        /// <summary>
        /// Gets the territory ownership color of this group of empty spaces.
        /// </summary>
        public Content Territory { get; internal set; }

        public Group()
        {
        }

        public void Clear()
        {
            pointsMask = 0;
            neighboursMask = 0;
            IsDead = false;
        }

        /// <summary>
        /// Constructs a group object of specified content.
        /// </summary>
        /// <param name="c">The group content.</param>
        public Group(Content c)
        {
            Content = c;
        }

        /// <summary>
        /// Adds a point to the group.
        /// </summary>
        /// <param name="x">The X coordinate of the point.</param>
        /// <param name="y">The Y coordinate of the point.</param>
        public void AddPoint(int x, int y)
        {
            pointsMask |= Board.GetCellMask(x, y, SizeX, SizeY);
        }

        /// <summary>
        /// Checks whether this group contains the specified point.
        /// </summary>
        /// <param name="x">The X coordinate of the point.</param>
        /// <param name="y">The Y coordinate of the point.</param>
        /// <returns>Returns true if the point is contained in the group.</returns>
        public bool ContainsPoint(int x, int y)
        {
            return (pointsMask & Board.GetCellMask(x, y, SizeX, SizeY)) != 0;
        }

        /// <summary>
        /// Adds a neighbour point to the group.
        /// </summary>
        /// <param name="x">The X coordinate of the neighbour.</param>
        /// <param name="y">The Y coordinate of the neighbour.</param>
        public void AddNeighbour(int x, int y)
        {
            neighboursMask |= Board.GetCellMask(x, y, SizeX, SizeY);
        }

        public bool AnyNeighbour(uint contentMask)
        {
            return (neighboursMask & contentMask) != 0;
        }
    }
}
