using FineGameDesign.Pooling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Go
{
    /// <summary>
    /// Encapsulates a board position, without any game context. This object also
    /// supports scoring mode by setting the IsScoring property to true.
    /// </summary>
    public sealed class Board
    {
        private const int kBlackIndex = 0;
        private const int kWhiteIndex = 1;

        private const int kMaxSize = 5;
        private const int kMaxCells = kMaxSize * kMaxSize;
        private const int kBothPlayerCells = 2 * kMaxCells;
        private const int kMaxGroups = kMaxCells / 2 + 1;

        public static ObjectPool<List<Group>> GroupListPool;
        public static ObjectPool<Group> GroupPool;

        static Board()
        {
            InitPools();
        }

        public static void InitPools()
        {
            if (ObjectPool<List<Group>>.TryInit(32))
            {
                GroupListPool = ObjectPool<List<Group>>.Shared;
            }

            if (ObjectPool<Group>.TryInit(32))
            {
                GroupPool = ObjectPool<Group>.Shared;
            }
        }

        /// <summary>
        /// Only valid for 32 cells or less.
        /// </summary>
        private uint[] playerCellMask = new uint[2];

        public uint GetContentMask(int playerIndex)
        {
            return playerCellMask[playerIndex];
        }

        public uint GetEmptyMask()
        {
            return ~(playerCellMask[kBlackIndex] | playerCellMask[kWhiteIndex]);
        }

        public ulong GetContentMask()
        {
            return playerCellMask[kBlackIndex] +
                (playerCellMask[kWhiteIndex] << kMaxCells);
        }

        public ulong GetContentAndMoveMask(int moveIndex)
        {
            return (ulong)(playerCellMask[kBlackIndex]) +
                (ulong)(playerCellMask[kWhiteIndex] << kMaxCells) +
                (ulong)(moveIndex << kBothPlayerCells);
        }

        public static uint GetCellMask(int x, int y, int SizeX, int SizeY)
        {
            int cellIndex = y * SizeX + x;
            int cellMask = 1 << cellIndex;
            return (uint)cellMask;
        }

        private void SetEmptyContentMask(uint cellMask)
        {
            uint inverseMask = ~cellMask;
            playerCellMask[kBlackIndex] &= inverseMask;
            playerCellMask[kWhiteIndex] &= inverseMask;
        }
        
        private void SetContentMask(int x, int y, Content c)
        {
            uint cellMask = GetCellMask(x, y, SizeX, SizeY);
            uint inverseMask = ~cellMask;
            switch (c)
            {
                case Content.Black:
                    playerCellMask[kBlackIndex] |= cellMask;
                    playerCellMask[kWhiteIndex] &= inverseMask;
                    break;
                case Content.White:
                    playerCellMask[kBlackIndex] &= inverseMask;
                    playerCellMask[kWhiteIndex] |= cellMask;
                    break;
                case Content.Empty:
                    playerCellMask[kBlackIndex] &= inverseMask;
                    playerCellMask[kWhiteIndex] &= inverseMask;
                    break;
                default:
                    throw new InvalidOperationException("Expected content handled.");
            }
        }

        private Group[,] groupCache2;
        private List<Group> groupCache = null;
        // TODO: private Group[] groupsArray = new Group[kMaxGroups];
        private bool _IsScoring = false;

        /// <summary>
        /// Gets the horizontal size of the board.
        /// Limited to 5x5 for optimization.
        /// </summary>
        private int _SizeX;
        public int SizeX {
            get { return _SizeX; }
            private set
            {
                _SizeX = value;
                Group.SizeX = value;
            }
        }

        /// <summary>
        /// Gets the vertical size of the board.
        /// Limited to 5x5 for optimization.
        /// </summary>
        private int _SizeY;
        public int SizeY {
            get { return _SizeY; }
            private set
            {
                _SizeY = value;
                Group.SizeY = value;
            }
        }

        /// <summary>
        /// Gets or sets a flag indicating whether this board is in scoring mode.
        /// If this property is changed from false to true, the scoring cache is cleared,
        /// and all dead groups are reinstated. To reset the scoring process, set this
        /// property to false and then to true again, or alternatively call ResetScoring.
        /// </summary>
        public bool IsScoring
        {
            get
            {
                return _IsScoring;
            }
            set
            {
                if (_IsScoring != value)
                {
                    _IsScoring = value;
                    UpdateScoring();
                }
            }
        }

        public void UpdateScoring()
        {
            if (IsScoring)
                CacheGroups();
        }

        /// <summary>
        /// Gets a Dictionary&lt;Content,int&gt; containing the score for each side. The score
        /// includes dead groups but does not include captured stones (no game context).
        /// </summary>
        public Dictionary<Content, int> Territory
        {
            get
            {
                Dictionary<Content, int> rc = new Dictionary<Content, int>();
                if (!IsScoring)
                    return rc;

                CacheGroups();

                int w = 0, b = 0;
                if (groupCache == null)
                {
                    #if DISABLE_CALC_TERRITORY
                    return rc;
                    #else
                    throw new InvalidOperationException("Expected group cache was defined.");
                    #endif
                }
                var emptyGroups = groupCache.Where(x => x.Content == Content.Empty);
                foreach (var p in emptyGroups)
                {
                    if (!p.AnyNeighbour(playerCellMask[kBlackIndex]))
                    {
                        w += p.NumPoints();
                    }
                    else if (!p.AnyNeighbour(playerCellMask[kWhiteIndex]))
                    {
                        b += p.NumPoints();
                    }
                }
                rc[Content.Black] = b;
                rc[Content.White] = w;
                return rc;
            }
        }

        public Board()
        {
        }

        /// <summary>
        /// Constructs a board object of specified horizontal and vertical size.
        /// </summary>
        /// <param name="sx">The horizontal size of the board.</param>
        /// <param name="sy">The vertical size of the board.</param>
        public Board(int sx, int sy)
        {
            SizeX = sx;
            SizeY = sy;
        }

        /// <summary>
        /// Constructs a board object from an existing board object, copying its size and content.
        /// </summary>
        /// <param name="fromBoard">The source board object.</param>
        public Board(Board fromBoard)
        {
            Clone(fromBoard);
        }

        /// <summary>
        /// Clones group caches.
        /// </summary>
        public void Clone(Board fromBoard)
        {
            if (SizeX != fromBoard.SizeX || SizeY != fromBoard.SizeY)
            {
                SizeX = fromBoard.SizeX;
                SizeY = fromBoard.SizeY;
            }

            if (SizeX < 1 || SizeX > 5)
            {
                throw new ArgumentOutOfRangeException("SizeX", "Invalid size x: " + SizeX);
            }
            if (SizeY < 1 || SizeY > 5)
            {
                throw new ArgumentOutOfRangeException("SizeY", "Invalid size y: " + SizeY);
            }
            Array.Copy(fromBoard.playerCellMask, playerCellMask, fromBoard.playerCellMask.Length);
            IsScoring = fromBoard.IsScoring;

            if (fromBoard.groupCache == null)
            {
                ClearGroupCache();
            }
            else
            {
                if (groupCache == null)
                {
                    groupCache = GroupListPool.Rent();
                }
                groupCache.Clear();
                groupCache.AddRange(fromBoard.groupCache);

                int numCells = fromBoard.groupCache2.Length;
                if (groupCache2 == null ||
                    groupCache2.GetLength(0) != SizeX || groupCache2.GetLength(1) != SizeY)
                {
                    groupCache2 = new Group[SizeX, SizeY];
                }
                Array.Copy(fromBoard.groupCache2, groupCache2, numCells);
            }
        }

        /// <summary>
        /// Gets or sets the board content at the specified point. Changing the board
        /// content using this property is not considered a game move, but rather a
        /// setup move.
        /// </summary>
        /// <param name="x">The X coordinate of the position.</param>
        /// <param name="y">The Y coordinate of the position.</param>
        /// <returns></returns>
        public Content this[int x, int y]
        {
            get
            {
                return GetContentAt(x, y);
            }
            set
            {
                SetContentAt(x, y, value);
            }
        }

        /// <summary>
        /// Gets or sets the board content at the specified point. Changing the board
        /// content using this property is not considered a game move, but rather a
        /// setup move.
        /// </summary>
        /// <param name="n">The coordinates of the position.</param>
        /// <returns></returns>
        public Content this[Point n]
        {
            get
            {
                return GetContentAt(n);
            }
            set
            {
                SetContentAt(n.x, n.y, value);
            }
        }

        /// <summary>
        /// Gets the board content at the specified point.
        /// </summary>
        /// <param name="n">The coordinates of the position.</param>
        /// <returns></returns>
        public Content GetContentAt(Point n)
        {
            return GetContentAt(n.x, n.y);
        }

        /// <summary>
        /// Gets the board content at the specified point.
        /// </summary>
        /// <param name="x">The X coordinate of the position.</param>
        /// <param name="y">The Y coordinate of the position.</param>
        /// <returns></returns>
        public Content GetContentAt(int x, int y)
        {
            uint cellMask = GetCellMask(x, y, SizeX, SizeY);
            if ((playerCellMask[kBlackIndex] & cellMask) != 0)
                return Content.Black;
            if ((playerCellMask[kWhiteIndex] & cellMask) != 0)
                return Content.White;
            return Content.Empty;
        }

        public Content GetContentAt(int cellIndex)
        {
            uint cellMask = (uint)(1 << cellIndex);
            if ((playerCellMask[kBlackIndex] & cellMask) != 0)
                return Content.Black;
            if ((playerCellMask[kWhiteIndex] & cellMask) != 0)
                return Content.White;
            return Content.Empty;
        }

        /// <summary>
        /// Sets the board content at the specified point, this is not considered a
        /// game move, but rather a setup move.
        /// </summary>
        /// <param name="p">The coordinates of the position.</param>
        /// <param name="content">The new content at the position.</param>
        public void SetContentAt(Point p, Content content)
        {
            SetContentAt(p.x, p.y, content);
        }

        /// <summary>
        /// Sets the board content at the specified point, this is not considered a
        /// game move, but rather a setup move.
        /// </summary>
        /// <param name="x">The X coordinate of the position.</param>
        /// <param name="y">The Y coordinate of the position.</param>
        /// <param name="c">The new content at the position.</param>
        public void SetContentAt(int x, int y, Content c)
        {
            if (x < 0 || x >= SizeX)
            {
                throw new ArgumentOutOfRangeException("x", "Invalid x coordinate: " + x);
            }
            if (y < 0 || y >= SizeY)
            {
                throw new ArgumentOutOfRangeException("y", "Invalid y coordinate: " + y);
            }

            SetContentMask(x, y, c);

            ClearGroupCache();
        }

        /// <summary>
        /// Gets the group including the board content at the specified position.
        /// </summary>
        /// <param name="n">The coordinates of the position.</param>
        /// <returns>A group object containing a list of points.</returns>
        public Group GetGroupAt(Point n)
        {
            return GetGroupAt(n.x, n.y);
        }

        /// <summary>
        /// Gets the group including the board content at the specified position.
        /// </summary>
        /// <param name="x">The X coordinate of the position.</param>
        /// <param name="y">The Y coordinate of the position.</param>
        /// <returns>A group object containing a list of points.</returns>
        public Group GetGroupAt(int x, int y)
        {
            if (groupCache == null)
            {
                groupCache = GroupListPool.Rent();
                groupCache.Clear();
                if (groupCache2 == null ||
                    groupCache2.GetLength(0) != SizeX || groupCache2.GetLength(1) != SizeY)
                {
                    groupCache2 = new Group[SizeX, SizeY];
                }
                else
                {
                    Array.Clear(groupCache2, 0, groupCache2.Length);
                }
            }

            Group group = null;
            foreach (Group cache in groupCache)
            {
                if (cache.ContainsPoint(x, y))
                {
                    group = cache;
                    break;
                }
            }
            if (group == null)
            {
                group = GroupPool.Rent();
                group.Clear();
                group.Content = this[x, y];
                RecursiveAddPoint(group, x, y);
                groupCache.Add(group);
            }
            return group;
        }

        private void RecursiveAddPoint(Group group, int x, int y)
        {
            if (GetContentAt(x, y) == group.Content)
            {
                if (group.ContainsPoint(x, y)) return;
                group.AddPoint(x, y);
                groupCache2[x, y] = group;
                if (x > 0) RecursiveAddPoint(group, x - 1, y);
                if (x < SizeX - 1) RecursiveAddPoint(group, x + 1, y);
                if (y > 0) RecursiveAddPoint(group, x, y - 1);
                if (y < SizeY - 1) RecursiveAddPoint(group, x, y + 1);
            }
            else
            {
                group.AddNeighbour(x, y);
            }
        }

        /// <summary>
        /// Gets the liberty count of the specified group.
        /// </summary>
        /// <param name="group">The group object.</param>
        /// <returns>The number of liberties of the specified group.</returns>
        public bool HasLiberties(Group group)
        {
            #if DISABLE_GROUP_POINTS
            return false;
            #endif
            if (group.Content == Content.Empty)
                return true;

            return group.AnyNeighbour(GetEmptyMask());
        }

        /// <summary>
        /// Gets the liberty count of the group containing the board content at
        /// the specified point.
        /// </summary>
        /// <param name="x">The X coordinate of the position.</param>
        /// <param name="y">The Y coordinate of the position.</param>
        /// <returns>The number of liberties.</returns>
        public bool HasLiberties(int x, int y)
        {
            #if DISABLE_GROUP_POINTS
            return false;
            #endif
            return HasLiberties(GetGroupAt(x, y));
        }

        private void CacheGroups()
        {
            #if DISABLE_CALC_TERRITORY
            return;
            #endif

            bool pass = true;
            while (pass)
            {
                pass = false;
                for (int i = 0; i < SizeX; i++)
                {
                    for (int j = 0; j < SizeY; j++)
                    {
                        if (groupCache == null || groupCache2 == null || groupCache2[i, j] == null)
                        {
                            GetGroupAt(i, j);
                            pass = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Resets the scoring process, unmarking dead groups.
        /// </summary>
        public void ResetScoring()
        {
            if (!IsScoring) return;
            ClearGroupCache();
            CacheGroups();
        }

        public bool WouldCapture(int x, int y, Content content)
        {
            #if DISABLE_GROUP_POINTS
            return false;
            #endif

            var stoneNeighbours = GetStoneNeighbours(x, y);
            foreach (var n in stoneNeighbours)
            {
                if (GetContentAt(n) == content)
                {
                    Group ngroup = GetGroupAt(n);
                    if (!HasLiberties(ngroup))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void GetCapturedGroups(int x, int y, List<Group> captures)
        {
            #if DISABLE_GROUP_POINTS
            return captures;
            #endif
            var stoneNeighbours = GetStoneNeighbours(x, y);
            foreach (var n in stoneNeighbours)
            {
                if (GetContentAt(n) != Content.Empty)
                {
                    Group ngroup = GetGroupAt(n);
                    if (ngroup.ContainsPoint(x, y)) continue; // Don't consider self group
                    if (!HasLiberties(ngroup))
                    {
                        if (!ngroup.AnyPointsIntersect(captures))
                            captures.Add(ngroup);
                    }
                }
            }
        }

        private List<Point> GetStoneNeighbours(int x, int y)
        {
            List<Point> rc = new List<Point>(4);
            if (x > 0) rc.Add(new Point(x - 1, y));
            if (x < SizeX - 1) rc.Add(new Point(x + 1, y));
            if (y > 0) rc.Add(new Point(x, y - 1));
            if (y < SizeY - 1) rc.Add(new Point(x, y + 1));
            return rc;
        }

        internal int Capture(IEnumerable<Group> captures)
        {
            int rc = 0;
            foreach (var g in captures)
            {
                rc += Capture(g);
            }
            return rc;
        }

        internal int Capture(Group g)
        {
            SetEmptyContentMask(g.PointsMask);

            return g.NumPoints();
        }

        private void ClearGroupCache()
        {
            if (groupCache != null)
            {
                foreach (Group group in groupCache)
                {
                    GroupPool.Return(group);
                }
                GroupListPool.Return(groupCache);
                groupCache = null;
            }
        }

        /// <summary>
        /// Returns a multi-line string representation of the board with the scoring
        /// state. Each spot is composed of two characters. The first is one of [.XO]
        /// representing an empty, black or white board content respectively. The second
        /// is one of [.xoD] representing unowned, black or white territory, or D for a
        /// dead group.
        /// </summary>
        /// <returns>Returns the multi-line string representation of the board.</returns>
        public override string ToString()
        {
            if (IsScoring)
                CacheGroups();

            string rc = "";
            for (int i = 0; i < SizeY; i++)
            {
                for (int j = 0; j < SizeX; j++)
                {
                    if (this[j, i] == Content.Empty) rc += ".";
                    else if (this[j, i] == Content.Black) rc += "X";
                    else rc += "O";
                    rc += " ";
                }
                rc += "\n";
            }
            return rc;
        }

        /// <summary>
        /// Represents a position and a content at that position.
        /// </summary>
        public struct PositionContent
        {
            /// <summary>
            /// The position point.
            /// </summary>
            public Point Position;

            /// <summary>
            /// The content at the position.
            /// </summary>
            public Content Content;

            public override string ToString()
            {
                return "PositionContent(" + Position + ", " + Content + ")";
            }
        }

        /// <summary>
        /// Returns an enumerable representing all occupied board spots.
        /// </summary>
        public IEnumerable<PositionContent> AllStones
        {
            get
            {
                for (int i = 0; i < SizeX; i++)
                {
                    for (int j = 0; j < SizeY; j++)
                    {
                        if (this[i, j] != Content.Empty)
                            yield return new PositionContent
                            {
                                Content = this[i, j],
                                Position = new Point(i, j)
                            };
                    }
                }
            }
        }

        public IEnumerable<PositionContent> AllCells
        {
            get
            {
                for (int i = 0; i < SizeX; i++)
                {
                    for (int j = 0; j < SizeY; j++)
                    {
                        yield return new PositionContent
                        {
                            Content = this[i, j],
                            Position = new Point(i, j)
                        };
                    }
                }
            }
        }

        public List<PositionContent> AllTerritory
        {
            get
            {
                var territories = new List<PositionContent>();
                #if !DISABLE_CALC_TERRITORY
                if (!IsScoring)
                #endif
                    return territories;

                CacheGroups();

                for (int i = 0; i < SizeX; i++)
                {
                    for (int j = 0; j < SizeY; j++)
                    {
                        Group g = groupCache2[i, j];
                        territories.Add(new PositionContent
                        {
                            Content = GetTerritory(g),
                            Position = new Point(i, j)
                        });
                    }
                }
                return territories;
            }
        }

        /// <summary>
        /// Gets the territory ownership color of this group of empty spaces.
        /// If not empty or not fully surrounded, returns empty.
        /// </summary>
        private Content GetTerritory(Group p)
        {
            if (p.Content != Content.Empty)
            {
                return Content.Empty;
            }
            if (!p.AnyNeighbour(playerCellMask[kBlackIndex]))
            {
                return Content.White;
            }
            else if (!p.AnyNeighbour(playerCellMask[kWhiteIndex]))
            {
                return Content.Black;
            }

            return Content.Empty;
        }
    }
}
