// Models/Piece.cs
using System;

namespace ChessGame.Models
{
	/// <summary>
	/// Base class for all chess pieces
	/// </summary>
	public abstract class Piece
	{
		public PieceColor Color { get; }
		public abstract char Symbol { get; }

		protected Piece(PieceColor color)
		{
			Color = color;
		}

		public abstract bool IsValidMove(Move move, Board board);

		public override string ToString()
		{
			return $"{Color} {GetType().Name}";
		}
	}
}

