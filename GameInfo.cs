﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Go
{
    /// <summary>
    /// Provides information used to create the root node of a game tree.
    /// </summary>
    public class GameInfo
    {
        /// <summary>
        /// Gets or sets the handicap value.
        /// </summary>
        public int Handicap { get; set; }

        /// <summary>
        /// Gets or sets the komi value.
        /// </summary>
        public double Komi { get; set; }

        /// <summary>
        /// Gets or sets the horizontal board size.
        /// </summary>
        public int BoardSizeX { get; set; }

        /// <summary>
        /// Gets or sets the vertical board size.
        /// </summary>
        public int BoardSizeY { get; set; }

        /// <summary>
        /// Gets or sets the color of the starting player.
        /// </summary>
        public Content StartingPlayer { get; set; }

        /// <summary>
        /// Constructs a default GameInfo object, with 0 handicap, 5.5 komi 19x19 board and
        /// black as the starting player.
        /// </summary>
        public GameInfo()
        {
            Komi = 5.5;
            StartingPlayer = Content.Black;
            Handicap = 0;
            BoardSizeX = BoardSizeY = 19;
        }
    }
}