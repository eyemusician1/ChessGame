// Models/Player.cs
using System;

namespace ChessGame.Models
{
	/// <summary>
	/// Base class for players
	/// </summary>
	public abstract class Player
	{
		public PieceColor Color { get; }

		protected Player(PieceColor color)
		{
			Color = color;
		}

		public abstract Move GetMove(Board board);

		public override string ToString()
		{
			return $"{Color} Player";
		}
	}
}

