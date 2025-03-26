// Models/King.cs
using System;

namespace ChessGame.Models
{
	/// <summary>
	/// King piece implementation
	/// </summary>
	public class King : Piece
	{
		public override char Symbol => Color == PieceColor.White ? 'K' : 'k';

		public King(PieceColor color) : base(color) { }

		public override bool IsValidMove(Move move, Board board)
		{
			// King movement logic (one square in any direction)
			int dx = Math.Abs(move.ToX - move.FromX);
			int dy = Math.Abs(move.ToY - move.FromY);

			// Normal king move
			if (dx <= 1 && dy <= 1)
			{
				Piece targetPiece = board.GetPiece(move.ToX, move.ToY);
				return targetPiece == null || targetPiece.Color != Color;
			}

			// Castling is handled in the Board class

			return false;
		}
	}
}

