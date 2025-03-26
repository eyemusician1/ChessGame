// Models/Queen.cs
using System;

namespace ChessGame.Models
{
	/// <summary>
	/// Queen piece implementation
	/// </summary>
	public class Queen : Piece
	{
		public override char Symbol => Color == PieceColor.White ? 'Q' : 'q';

		public Queen(PieceColor color) : base(color) { }

		public override bool IsValidMove(Move move, Board board)
		{
			// Queen movement logic (combination of rook and bishop)
			int dx = Math.Abs(move.ToX - move.FromX);
			int dy = Math.Abs(move.ToY - move.FromY);

			// Not a straight or diagonal line
			if (dx != 0 && dy != 0 && dx != dy)
				return false;

			// Check for pieces in the path
			int signX = Math.Sign(move.ToX - move.FromX);
			int signY = Math.Sign(move.ToY - move.FromY);

			int x = move.FromX + signX;
			int y = move.FromY + signY;

			while (x != move.ToX || y != move.ToY)
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

