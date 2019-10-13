using FineGameDesign.Pooling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Go
{
    /// <summary>
    /// Represents a game tree complete with variations. A game tree root is identified
    /// by a non-null GameInfo property. The root of a specified game may be obtained
    /// using the Root property.
    /// </summary>
    public sealed class Game
    {
        public static ObjectPool<Game> GamePool;
        public static ObjectPool<Board> BoardPool;

        public static void InitPools()
        {
            if (ObjectPool<Game>.TryInit(8) || GamePool == null)
            {
                GamePool = ObjectPool<Game>.Shared;
            }

            if (ObjectPool<Board>.TryInit(1) || BoardPool == null)
            {
                BoardPool = ObjectPool<Board>.Shared;
            }

            Board.InitPools();
        }

        /// <summary>
        /// Represents a 'pass' move.
        /// </summary>
        public static readonly Point PassMove = new Point(-1, -1);

        public float Komi = 5.5f;

        private Dictionary<Content, int> captures = new Dictionary<Content, int>()
        {
            { Content.Black, 0 },
            { Content.White, 0 }
        };

        /// <summary>
        /// Not relative to the other player at this stage.
        /// Komi for white.
        /// Territory if board is scoring.
        /// Subtracting captures.
        /// </summary>
        public float GetScore(Content player)
        {
            if (player == Content.Empty)
                return 0f;

            Content other = GetOtherPlayer(player);
            int captured = captures[other];

            float score = -captured;

            if (player == Content.White)
                score += Komi;

            if (Board.IsScoring)
            {
                var territory = Board.Territory;
                if (territory.ContainsKey(player))
                    score += Board.Territory[player];
            }

            return score;
        }

        private Content GetOtherPlayer(Content player)
        {
            return player == Content.Black ? Content.White : Content.Black;
        }

        public double GetResult(Content player)
        {
            #if DISABLE_CALC_TERRITORY
            return 0.5;
            #endif
            if (!Board.IsScoring)
                return 0.5;

            float score = GetScore(player);
            float otherScore = GetScore(GetOtherPlayer(player));
            return score < otherScore ? 0.0 : (score > otherScore ? 1.0 : 0.5);
        }

        private HashSet<ulong> superKoSet = new HashSet<ulong>();

        /// <summary>
        /// Gets the board object of the current game position.
        /// </summary>
        public Board Board { get; private set; }

        /// <summary>
        /// Gets the color of the player whose turn it is to play.
        /// </summary>
        public Content Turn { get; private set; }

        /// <summary>
        /// Gets a flag indicating whether the move used to reach this board was legal.
        /// This property may be null if this board position is the result of only setup moves.
        /// </summary>
        public bool? IsLegal { get; private set; }

        /// <summary>
        /// Gets the number of stones white has captured
        /// (the number of black stones captured).
        /// </summary>
        public int WhiteCaptures { get { return captures[Content.White]; } }

        /// <summary>
        /// Gets the number of stones black has captured
        /// (the number of white stones captured).
        /// </summary>
        public int BlackCaptures { get { return captures[Content.Black]; } }

        public Game()
        {
        }

        /// <summary>
        /// Constructs a Game object from an existing Game object. This constructor is used when making
        /// game moves.
        /// </summary>
        /// <param name="fromGame">The Game object before the move.</param>
        /// <param name="cloneTurn">Otherwise, sets opposite turn.</param>
        public void Clone(Game fromGame, bool cloneTurn = false)
        {
            m_NumPasses = fromGame.m_NumPasses;
            m_Ended = fromGame.Ended;

            Board = new Board();
            Board.Clone(fromGame.Board);
            if (cloneTurn)
            {
                Turn = fromGame.Turn;
            }
            else
            {
                Turn = fromGame.Turn.Opposite();
            }
            #if !DISABLE_CAPTURES_DICTIONARY
            captures[Content.White] = fromGame.captures[Content.White];
            captures[Content.Black] = fromGame.captures[Content.Black];
            #endif
            superKoSet.Clear();
            foreach (var p in fromGame.superKoSet) superKoSet.Add(p);
        }

        /// <summary>
        /// Constructs a Game object from a Board object and a turn to play.
        /// </summary>
        /// <param name="bs">The source Board.</param>
        /// <param name="turn">The color of the player whose turn it is to play.</param>
        public Game(Board bs, Content turn)
        {
            Board = new Board(bs);
            Turn = turn;
        }

        /// <summary>
        /// Makes a move and returns a new Game object representing the state after the
        /// move. The move is carried out whether it is legal or illegal (for example,
        /// an overwrite move). The color of the move is determined by the Turn property.
        /// The legality of the move may be determined by examining the IsLegal property
        /// of the returned object.
        /// </summary>
        /// <param name="n">The coordinates of the move.</param>
        /// <returns>A game object representing the state of the game after the move.</returns>
        public Game MakeMove(Point n)
        {
            return MakeMove(n.x, n.y);
        }

        /// <summary>
        /// Makes a move and returns a new Game object representing the state after the
        /// move. The move is carried out whether it is legal or illegal (for example,
        /// an overwrite move). The color of the move is determined by the Turn property.
        /// If the move was illegal (suicide, violating super-ko, or an overwrite), the
        /// method sets the legal parameter to false, otherwise it is set to true.
        /// </summary>
        /// <param name="n">The coordinates of the move.</param>
        /// <param name="legal">Set to true if the move was legal, false otherwise.</param>
        /// <returns>A game object representing the state of the game after the move.</returns>
        public Game MakeMove(Point n, out bool legal)
        {
            return MakeMove(n.x, n.y, out legal);
        }

        /// <summary>
        /// Makes a move and returns a new Game object representing the state after the
        /// move. The move is carried out whether it is legal or illegal (for example,
        /// an overwrite move). The color of the move is determined by the Turn property.
        /// The legality of the move may be determined by examining the IsLegal property
        /// of the returned object.
        /// </summary>
        /// <param name="x">The X coordinate of the move.</param>
        /// <param name="y">The Y coordinate of the move.</param>
        /// <returns>A game object representing the state of the game after the move.</returns>
        public Game MakeMove(int x, int y)
        {
            bool dummy;
            return MakeMove(x, y, out dummy);
        }

        /// <summary>
        /// Makes a move and returns a new Game object representing the state after the
        /// move. The move is carried out whether it is legal or illegal (for example,
        /// an overwrite move). The color of the move is determined by the Turn property.
        /// If the move was illegal (suicide, violating super-ko, or an overwrite), the
        /// method sets the legal parameter to false, otherwise it is set to true.
        /// </summary>
        /// <param name="x">The X coordinate of the move.</param>
        /// <param name="y">The Y coordinate of the move.</param>
        /// <param name="legal">Set to true if the move was legal, false otherwise.</param>
        /// <returns>A game object representing the state of the game after the move.</returns>
        public Game MakeMove(int x, int y, out bool legal)
        {
            if (x < 0 && y < 0)
            {
                legal = true;
                return Pass();
            }

            if (m_NumPasses > 0)
                m_NumPasses--;

            var g = new Game();
            g.Clone(this);
            legal = g.InternalMakeMove(x, y);
            return g;
        }

        private const int kMaxPasses = 1;
        /// <summary>
        /// Consecutively passing more than max ends game.
        /// </summary>
        private int m_NumPasses;
        private bool m_Ended;
        public bool Ended
        {
            get { return m_Ended; }
        }

        /// <summary>
        /// Makes a 'pass' move and returns a new Game object representing the state after 
        /// the move. The color of the move is determined by the Turn property.
        /// </summary>
        /// <returns>A game object representing the state of the game after the move.</returns>
        public Game Pass()
        {
            m_NumPasses++;
            m_Ended = m_NumPasses > kMaxPasses;
            if (m_Ended)
            {
                Board.IsScoring = true;
            }

            var g = new Game();
            g.Clone(this);
            return g;
        }

        /// <summary>
        /// Perform the necessary operations for a move, check liberties, capture, etc. Also
        /// updates the Move and IsLegal properties.
        /// </summary>
        /// <param name="x">The X coordinate of the move.</param>
        /// <param name="y">The Y coordinate of the move.</param>
        /// <returns>True if the move was legal.</returns>
        protected bool InternalMakeMove(int x, int y)
        {
            bool legal = true;
            Content oturn = Turn.Opposite();
            if (Board[x, y] != Content.Empty) legal = false; // Overwrite move
            Board[x, y] = oturn;
            List<Group> capturedGroups = Board.GroupListPool.Rent();
            capturedGroups.Clear();
            Board.GetCapturedGroups(x, y, capturedGroups);
            if (capturedGroups.Count == 0 && !Board.HasLiberties(x, y)) // Suicide move
            {
                captures[Turn] += Board.Capture(Board.GetGroupAt(x, y));
                legal = false;
            }
            else captures[oturn] += Board.Capture(capturedGroups.Where(p => p.Content == oturn.Opposite()));

            if (!superKoSet.Add(Board.GetContentMask())) // Violates super-ko
                legal = false;
            
            IsLegal = legal;
            Board.GroupListPool.Return(capturedGroups);
            return legal;
        }

        private static readonly List<Point> s_Empty = new List<Point>();

        /// <returns>
        /// If scoring, no moves.
        ///
        /// Excludes pass move, unless no other move is legal or previous player passed.
        /// So an AI-player will keep playing until it cannot move, then the other AI player might stop too.
        /// Otherwise, since scoring is not smart, some territory will not be owned.
        /// Smart territory calculation needs pretty good AI.
        /// </returns>
        /// <param name="cloneTurn">Otherwise, gets moves for opposite player's turn.</param>
        public List<Point> GetLegalMoves(bool cloneTurn = false)
        {
            if (Board == null || Board.IsScoring)
                return s_Empty;

            List<Point> moves = new List<Point>();
            Content turn = cloneTurn ? Turn : Turn.Opposite();

            for (int x = 0; x < Board.SizeX; x++)
            {
                for (int y = 0; y < Board.SizeY; y++)
                {
                    if (Board[x, y] != Content.Empty)
                        continue;

                    Board hypotheticalBoard = BoardPool.Rent();
                    List<Group> capturedGroups = Board.GroupListPool.Rent();
                    Board.GetHypotheticalCapturedGroups(hypotheticalBoard, capturedGroups, x, y, turn);
                    if (capturedGroups.Count == 0 && !hypotheticalBoard.HasLiberties(x, y)) // Suicide move
                    {
                        Board.GroupListPool.Return(capturedGroups);
                        BoardPool.Return(hypotheticalBoard);

                        continue;
                    }

                    if (capturedGroups.Count != 0)
                    {
                        hypotheticalBoard.Capture(capturedGroups.Where(p => p.Content == turn.Opposite()));
                        if (superKoSet.Contains(hypotheticalBoard.GetContentMask())) // Violates super-ko
                        {
                            Board.GroupListPool.Return(capturedGroups);
                            BoardPool.Return(hypotheticalBoard);
                            continue;
                        }
                    }

                    moves.Add(new Point(x, y));
                    Board.GroupListPool.Return(capturedGroups);
                    BoardPool.Return(hypotheticalBoard);
                }
            }

            if (moves.Count == 0 || m_NumPasses > 0)
            {
                if (!Board.IsScoring)
                {
                    moves.Add(PassMove);
                }
            }

            return moves;
        }
    }
}
