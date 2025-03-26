// Models/Rook.cs
using System;

namespace ChessGame.Models
{
	/// <summary>
	/// Rook piece implementation
	/// </summary>
	public class Rook : Piece
	{
		public override char Symbol => Color == PieceColor.White ? 'R' : 'r';

		public Rook(PieceColor color) : base(color) { }

		public override bool IsValidMove(Move move, Board board)
		{
			// Rook movement logic (horizontal and vertical)
			if (move.FromX != move.ToX && move.FromY != move.ToY)
				return false;

			// Check for pieces in the path
			int dx = Math.Sign(move.ToX - move.FromX);
			int dy = Math.Sign(move.ToY - move.FromY);

			int x = move.FromX + dx;
			int y = move.FromY + dy;

			while (x != move.ToX || y != move.ToY)
			{
				if (board.GetPiece(x, y) != null)
					return false;

				x += dx;
				y += dy;
			}

			// Check destination square
			Piece targetPiece = board.GetPiece(move.ToX, move.ToY);
			return targetPiece == null || targetPiece.Color != Color;
		}
	}
}

