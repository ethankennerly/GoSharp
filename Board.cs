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
    public class Board
    {
        private const int kBlackIndex = 0;
        private const int kWhiteIndex = 1;
        private const Content kPlayer0 = Content.Black;

        public static ObjectPool<List<Group>> GroupListPool;

        public static void InitPools()
        {
            if (ObjectPool<List<Group>>.TryInit(32))
            {
                GroupListPool = ObjectPool<List<Group>>.Shared;
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

        private Content[,] content;
        private Group[,] groupCache2;
        private List<Group> groupCache = null;
        private bool _IsScoring = false;
        private int? _Hash = null;

        /// <summary>
        /// Gets the horizontal size of the board.
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
            ClearGroupCache();
            if (IsScoring)
                CalcTerritory();
        }

        /// <summary>
        /// Gets a Dictionary&lt;Content,int&gt; containing the score for each side. The score
        /// includes dead groups but does not include captured stones (no game context).
        /// If SetDeadGroup is called, this property must be retrieved again to get
        /// the updated score.
        /// </summary>
        public Dictionary<Content, int> Territory
        {
            get
            {
                Dictionary<Content, int> rc = new Dictionary<Content, int>();
                if (!IsScoring)
                    return rc;

                CalcTerritory();

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
                    if (p.Neighbours.All(x => GetContentAt(x) != Content.Black))
                    {
                        w += p.NumPoints();
                        p.Territory = Content.White;
                    }
                    else if (p.Neighbours.All(x => GetContentAt(x) != Content.White))
                    {
                        b += p.NumPoints();
                        p.Territory = Content.Black;
                    }
                    else p.Territory = Content.Empty;
                }
                foreach (var p in groupCache.Where(x => x.IsDead))
                {
                    if (p.Content == Content.Black)
                        w += p.NumPoints() * 2;
                    else if (p.Content == Content.White)
                        b += p.NumPoints() * 2;
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
            content = new Content[sx, sy];
            SizeX = sx;
            SizeY = sy;
        }

        /// <summary>
        /// Constructs a board object from an existing board object, copying its size and content.
        /// </summary>
        /// <param name="fromBoard">The source board object.</param>
        public Board(Board fromBoard)
        {
            SizeX = fromBoard.SizeX;
            SizeY = fromBoard.SizeY;
            #if DISABLE_ARRAY_COPY
            content = fromBoard.content;
            #else
            content = new Content[SizeX, SizeY];
            Array.Copy (fromBoard.content, content, content.Length);
            #endif
            IsScoring = fromBoard.IsScoring;
        }

        public void Clone(Board fromBoard)
        {
            if (SizeX != fromBoard.SizeX || SizeY != fromBoard.SizeY)
            {
                SizeX = fromBoard.SizeX;
                SizeY = fromBoard.SizeY;
                #if DISABLE_ARRAY_COPY
                content = fromBoard.content;
                #else
                content = new Content[SizeX, SizeY];
                #endif
            }
            Array.Copy(fromBoard.content, content, content.Length);
            IsScoring = fromBoard.IsScoring;
            ClearGroupCache();
        }

        /// <summary>
        /// Construct a board object from a parameter array. Each parameter may be
        /// 0 for empty, 1 for black or 2 for white, and the number of parameters must
        /// be a square of a natural number. The board size will be a square whose side
        /// length is the square root of the number of parameters.
        /// </summary>
        /// <param name="c">The board content (0-empty, 1-black, 2-white).</param>
        public Board(params int[] c)
        {
            if (c.Length == 0)
                throw new InvalidOperationException("Must provide some arguments.");
            double d = Math.Sqrt(c.Length);
            int id = (int)d;
            if (id != d)
                throw new InvalidOperationException("Argument count must be a square of a natural number.");
            SizeX = SizeY = id;
            content = new Content[id, id];
            int y = 0, x = 0;
            for (int i = 0; i < c.Length; i++)
            {
                content[x, y] = (Content)c[i];
                x++;
                if (x == SizeX)
                {
                    x = 0;
                    y++;
                }
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
            #if !DISABLE_GROUP_POINTS
            if (IsScoring && content[x, y] != Content.Empty && groupCache2[x, y] != null && groupCache2[x, y].IsDead)
                return Content.Empty;
            #endif

            return content[x, y];

            uint cellMask = GetCellMask(x, y, SizeX, SizeY);
            if ((playerCellMask[kBlackIndex] & cellMask) != 0)
                return Content.Black;
            if ((playerCellMask[kWhiteIndex] & cellMask) != 0)
                return Content.White;
            return Content.Empty;
        }

        public Content GetContentAt(int cellIndex)
        {
            int x = cellIndex % SizeX;
            int y = cellIndex / SizeX;
            return content[x, y];

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
                throw new ArgumentOutOfRangeException("x", "Invalid x coordinate.");
            }
            if (y < 0 || y >= SizeY)
            {
                throw new ArgumentOutOfRangeException("y", "Invalid y coordinate.");
            }
            content[x, y] = c;
            _Hash = null;
            ClearGroupCache();

            SetContentMask(x, y, c);
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
                if (groupCache2 == null)
                {
                    groupCache2 = new Group[SizeX, SizeY];
                }
                else
                {
                    Array.Clear(groupCache2, 0, groupCache2.Length);
                }
            }
            Group group = groupCache.SingleOrDefault(z => z.ContainsPoint(x, y));
            if (group == null)
            {
                group = new Group(content[x, y]);
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
                return group.NumPoints() > 0;

            foreach (var n in group.Neighbours)
            {
                if (GetContentAt(n) == Content.Empty) return true;
            }
            return false;
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

        /// <summary>
        /// Gets the liberty count of the specified group.
        /// </summary>
        /// <param name="group">The group object.</param>
        /// <returns>The number of liberties of the specified group.</returns>
        public int GetLiberties(Group group)
        {
            #if DISABLE_GROUP_POINTS
            return 0;
            #endif
            if (group.Content == Content.Empty)
                return group.NumPoints();

            int libs = 0;
            foreach (var n in group.Neighbours)
            {
                if (GetContentAt(n) == Content.Empty) libs++;
            }
            return libs;
        }

        /// <summary>
        /// Gets the liberty count of the group containing the board content at
        /// the specified point.
        /// </summary>
        /// <param name="x">The X coordinate of the position.</param>
        /// <param name="y">The Y coordinate of the position.</param>
        /// <returns>The number of liberties.</returns>
        public int GetLiberties(int x, int y)
        {
            #if DISABLE_GROUP_POINTS
            return 0;
            #endif
            return GetLiberties(GetGroupAt(x, y));
        }

        /// <summary>
        /// Gets the liberty count of the group containing the board content at
        /// the specified point.
        /// </summary>
        /// <param name="n">The coordinates of the position.</param>
        /// <returns>The number of liberties.</returns>
        public int GetLiberties(Point n)
        {
            return GetLiberties(n.x, n.y);
        }

        private void CalcTerritory()
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
        /// Marks a group as dead for the purposes of scoring. This method has no effect if
        /// the board is not in scoring mode (see the IsScoring property).
        /// </summary>
        /// <param name="n">The coordinates of the position of a stone in the group.</param>
        public void SetDeadGroup(Point n)
        {
            SetDeadGroup(n.x, n.y);
        }

        /// <summary>
        /// Marks a group as dead for the purposes of scoring. This method has no effect if
        /// the board is not in scoring mode (see the IsScoring property).
        /// </summary>
        /// <param name="x">The X coordinate of a position belonging to the group.</param>
        /// <param name="y">The Y coordinate of a position belonging to the group.</param>
        public void SetDeadGroup(int x, int y)
        {
            Group g = GetGroupAt(x, y);
            if (g.Content == Content.Empty) return;
            g.IsDead = !g.IsDead;
        }

        /// <summary>
        /// Resets the scoring process, unmarking dead groups.
        /// </summary>
        public void ResetScoring()
        {
            if (!IsScoring) return;
            ClearGroupCache();
            CalcTerritory();
        }

        internal bool IsSuicide(int x, int y)
        {
            if (HasLiberties(x, y))
                return false;

            List<Group> captures = GroupListPool.Rent();
            captures.Clear();
            GetCapturedGroups(x, y, captures);
            if (captures.Count == 0)
            {
                GroupListPool.Return(captures);
                return true;
            }

            GroupListPool.Return(captures);
            return false;
        }

        internal void GetCapturedGroups(int x, int y, List<Group> captures)
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
                    if (GetLiberties(ngroup) == 0)
                    {
                        if (!ngroup.AnyPointsIntersect(captures))
                            captures.Add(ngroup);
                    }
                }
            }
        }

        private List<Point> GetStoneNeighbours(int x, int y)
        {
            List<Point> rc = new List<Point>();
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
            foreach (var p in g.Points)
                SetContentAt(p, Content.Empty);

            SetEmptyContentMask(g.PointsMask);

            return g.NumPoints();
        }

        private void ClearGroupCache()
        {
            if (groupCache != null)
            {
                GroupListPool.Return(groupCache);
                groupCache = null;
            }
        }

        private int GetContentHashCode()
        {
            int hc = 0, tmp;
            foreach (var i in content)
            {
                tmp = hc >> 30;
                hc <<= 2;
                hc ^= (int)i ^ tmp;
            }
            return hc;
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
                CalcTerritory();

            string rc = "";
            for (int i = 0; i < SizeY; i++)
            {
                for (int j = 0; j < SizeX; j++)
                {
                    if (content[j, i] == Content.Empty) rc += ".";
                    else if (content[j, i] == Content.Black) rc += "X";
                    else rc += "O";
                    if (IsScoring)
                    {
                        Group g = groupCache2[j, i];
                        if (g.IsDead) rc += "D";
                        else if (g.Territory == Content.Empty) rc += ".";
                        else if (g.Territory == Content.Black) rc += "x";
                        else if (g.Territory == Content.White) rc += "o";
                    }
                    rc += " ";
                }
                rc += "\n";
            }
            return rc;
        }

        /// <summary>
        /// Gets a hash code of this board. Hash code includes board content.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            if (_Hash == null)
            {
                _Hash = GetContentHashCode();
            }
            return _Hash.Value;
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
                        if (content[i, j] != Content.Empty)
                            yield return new PositionContent
                            {
                                Content = content[i, j],
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
                            Content = content[i, j],
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

                CalcTerritory();

                for (int i = 0; i < SizeX; i++)
                {
                    for (int j = 0; j < SizeY; j++)
                    {
                        Group g = groupCache2[i, j];
                        territories.Add(new PositionContent
                        {
                            Content = g.Territory,
                            Position = new Point(i, j)
                        });
                    }
                }
                return territories;
            }
        }

        /// <summary>
        /// Returns an enumerable representing all empty board spots.
        /// </summary>
        public IEnumerable<Point> EmptySpaces
        {
            get
            {
                for (int i = 0; i < SizeX; i++)
                {
                    for (int j = 0; j < SizeY; j++)
                    {
                        if (content[i, j] == Content.Empty)
                            yield return new Point(i, j);
                    }
                }
            }
        }
    }
}
