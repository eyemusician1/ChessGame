// Models/Bishop.cs
using System;
using System.Windows.Documents;

namespace ChessGame.Models
{
	/// <summary>
	/// Bishop piece implementation
	/// </summary>
	public class Bishop : Piece
	{
		public override char Symbol => Color == PieceColor.White ? 'B' : 'b';

		public Bishop(PieceColor color) : base(color) { }

		public override bool IsValidMove(Move move, Board board)
		{
			// Bishop movement logic (diagonal)
			int dx = Math.Abs(move.ToX - move.FromX);
			int dy = Math.Abs(move.ToY - move.FromY);

			if (dx != dy)
				return false;

			// Check for pieces in the path
			int signX = Math.Sign(move.ToX - move.FromX);
			int signY = Math.Sign(move.ToY - move.FromY);

			int x = move.FromX + signX;
			int y = move.FromY + signY;

			while (x != move.ToX && y != move.ToY)
			{
				if (board.GetPiece(x, y) != null)
					return false;

				x += signX;
				y += signY;
			}

			// Check destination square
			Piece targetPiece = board.GetPiece(move.ToX, move.ToY);
			return targetPiece == null || targetPiece.Color != Color;
		}
	}
}

