using FineGameDesign.Pooling;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;

using Debug = UnityEngine.Debug;

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
            if (ObjectPool<Game>.TryInit(64) || GamePool == null)
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

        public float Komi
        {
            get
            {
                if (Board == null)
                {
                    return 0f;
                }

                if (Board.SizeX < 5 || Board.SizeY < 5)
                {
                    return 1.5f;
                }

                return 5.5f;
            }
        }

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
            float score = -captures[other];

            if (player == Content.White)
                score += Komi;

            if (Board != null && Board.IsScoring)
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

        /// <summary>
        /// Stores previous stone layout up to 5x5 and move that was made.
        /// 25 bits: black stone bitmask.
        /// 25 bits: white stone bitmask.
        /// 5 bits: move index.
        /// If current stone layout equals any previous stone layout, those moves are illegal.
        /// This memoization is faster than computing captures.
        ///
        /// Technically, some degenerate scenarios can repeat stone layouts from other inputs.
        /// <see cref="Go.UniTests.GameTests"/> with <c>STRICT_KO</c>.
        /// Yet, practically the best tactics are still rewarded.
        /// </summary>
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
            Board = new Board(fromGame.Board);

            m_NumPasses = fromGame.m_NumPasses;
            m_Ended = fromGame.Ended;

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

            IsLegal = fromGame.IsLegal;
        }

        /// <summary>
        /// Constructs a Game object from a Board object and a turn to play.
        /// </summary>
        /// <param name="bs">The source Board.</param>
        /// <param name="turn">The color of the player whose turn it is to play.</param>
        public void Clone(Board bs, Content turn)
        {
            Board = new Board(bs);
            Turn = turn;
            superKoSet.Clear();
            IsLegal = true;
            m_NumPasses = 0;
            m_Ended = false;
            captures[Content.White] = 0;
            captures[Content.Black] = 0;
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
        public Game MakeMove(int x, int y, Game nextGame)
        {
            bool dummy;
            return MakeMove(x, y, out dummy, nextGame);
        }

        public Game MakeMove(Point p, Game nextGame)
        {
            bool dummy;
            return MakeMove(p.x, p.y, out dummy, nextGame);
        }

        public Game MakeLegalMove(Point p, Game nextGame)
        {
            if (p.x < 0 && p.y < 0)
            {
                return Pass(nextGame);
            }


            if (m_NumPasses > 0)
                m_NumPasses--;

            IsLegal = true;
            nextGame.Clone(this, cloneTurn: true);
            nextGame.MakeLegalMove(p);
            return nextGame;
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
        public Game MakeMove(int x, int y, out bool legal, Game nextGame)
        {
            if (x < 0 && y < 0)
            {
                legal = true;
                return Pass(nextGame);
            }

            if (m_NumPasses > 0)
                m_NumPasses--;

            nextGame.Clone(this);
            legal = nextGame.InternalMakeMove(x, y);
            nextGame.IsLegal = legal;
            return nextGame;
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
        public Game Pass(Game nextGame)
        {
            m_NumPasses++;
            m_Ended = m_NumPasses > kMaxPasses;
            if (m_Ended)
            {
                Board.IsScoring = true;
            }

            nextGame.Clone(this);
            return nextGame;
        }

        /// <summary>
        /// Perform the necessary operations for a move, check liberties, capture, etc. Also
        /// updates the Move and IsLegal properties.
        /// </summary>
        /// <param name="x">The X coordinate of the move.</param>
        /// <param name="y">The Y coordinate of the move.</param>
        /// <returns>True if the move was legal.</returns>
        private bool InternalMakeMove(int x, int y)
        {
            bool legal = Board[x, y] == Content.Empty; // Overwrite move

            int moveIndex = y * Board.SizeX + x;
            ulong previousContentAndMoveMask = Board.GetContentAndMoveMask(moveIndex);
            if (!superKoSet.Add(previousContentAndMoveMask)) // Violates super-ko
                legal = false;

            Content oturn = Turn.Opposite();
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

            IsLegal = legal;
            Board.GroupListPool.Return(capturedGroups);
            return legal;
        }

        private void MakeLegalMove(Point p)
        {
            int moveIndex = p.y * Board.SizeX + p.x;
            ulong previousContentAndMoveMask = Board.GetContentAndMoveMask(moveIndex);
            superKoSet.Add(previousContentAndMoveMask);

            Content oturn = Turn.Opposite();
            Board[p.x, p.y] = Turn;
            List<Group> capturedGroups = Board.GroupListPool.Rent();
            capturedGroups.Clear();
            Board.GetCapturedGroups(p.x, p.y, capturedGroups);
            foreach (Group capturedGroup in capturedGroups)
            {
                int numCaptures = Board.Capture(capturedGroup);
                if (capturedGroup.Content == Turn)
                {
                    captures[oturn] += numCaptures;
                }
                else if (capturedGroup.Content == oturn)
                {
                    captures[Turn] += numCaptures;
                }
            }
            Board.GroupListPool.Return(capturedGroups);
            Turn = oturn;
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
        public List<Point> GetLegalMoves(bool cloneTurn = true)
        {
            if (Board == null || Board.IsScoring || Ended)
                return s_Empty;

            List<Point> moves = new List<Point>();
            Content turn = cloneTurn ? Turn : Turn.Opposite();
            Content oturn = cloneTurn ? Turn.Opposite() : Turn;

            for (int x = 0; x < Board.SizeX; x++)
            {
                for (int y = 0; y < Board.SizeY; y++)
                {
                    if (Board[x, y] != Content.Empty)
                        continue;

                    int moveIndex = y * Board.SizeX + x;
                    ulong previousContentAndMoveMask = Board.GetContentAndMoveMask(moveIndex);
                    if (superKoSet.Contains(previousContentAndMoveMask)) // Violates super-ko
                    {
                        Log("Game.GetLegalMoves: Super-ko: " + turn + "(" + x + "," + y + ")");
                        continue;
                    }

                    Board[x, y] = turn;
                    bool hasLiberties = Board.HasLiberties(x, y);
                    bool wouldCapture = Board.WouldCapture(x, y, oturn);
                    if (!(hasLiberties || wouldCapture))
                    {
                        Log("Game.GetLegalMoves: Suicide: " + turn + "(" + x + "," + y + ")");
                    }
                    else
                    {
                        Log("Game.GetLegalMoves: Legal: " + turn + "(" + x + "," + y + ")" +
                            "\nhasLiberties=" + hasLiberties + " wouldCapture=" + wouldCapture);
                        moves.Add(new Point(x, y));
                    }
                    Board[x, y] = Content.Empty;
                }
            }

            if (moves.Count == 0 || m_NumPasses > 0)
            {
                if (!Board.IsScoring)
                {
                    Log("Game.GetLegalMoves: Pass: " + turn);
                    moves.Add(PassMove);
                }
            }

            return moves;
        }

        [Conditional("LOG_GO_GAME")]
        private void Log(string message)
        {
            Debug.Log(message + ((Board == null) ? "" : "\nBoard:\n" + Board.ToString()));
        }
    }
}
