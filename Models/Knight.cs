// Models/Knight.cs
using System;

namespace ChessGame.Models
{
	/// <summary>
	/// Knight piece implementation
	/// </summary>
	public class Knight : Piece
	{
		public override char Symbol => Color == PieceColor.White ? 'N' : 'n';

		public Knight(PieceColor color) : base(color) { }

		public override bool IsValidMove(Move move, Board board)
		{
			// Knight movement logic (L-shape)
			int dx = Math.Abs(move.ToX - move.FromX);
			int dy = Math.Abs(move.ToY - move.FromY);

			if ((dx == 1 && dy == 2) || (dx == 2 && dy == 1))
			{
				Piece targetPiece = board.GetPiece(move.ToX, move.ToY);
				return targetPiece == null || targetPiece.Color != Color;
			}

			return false;
		}
	}
}

