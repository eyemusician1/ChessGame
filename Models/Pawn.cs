// Models/Pawn.cs
using System;

namespace ChessGame.Models
{
	/// <summary>
	/// Pawn piece implementation
	/// </summary>
	public class Pawn : Piece
	{
		public override char Symbol => Color == PieceColor.White ? 'P' : 'p';

		public Pawn(PieceColor color) : base(color) { }

		public override bool IsValidMove(Move move, Board board)
		{
			// Pawn movement logic
			int direction = Color == PieceColor.White ? 1 : -1;
			int startRow = Color == PieceColor.White ? 1 : 6;

			// Forward movement
			if (move.FromX == move.ToX && move.ToY == move.FromY + direction &&
				board.GetPiece(move.ToX, move.ToY) == null)
				return true;

			// Initial double move
			if (move.FromX == move.ToX && move.FromY == startRow &&
				move.ToY == move.FromY + 2 * direction &&
				board.GetPiece(move.ToX, move.FromY + direction) == null &&
				board.GetPiece(move.ToX, move.ToY) == null)
				return true;

			// Capture
			if (Math.Abs(move.ToX - move.FromX) == 1 && move.ToY == move.FromY + direction)
			{
				Piece targetPiece = board.GetPiece(move.ToX, move.ToY);
				if (targetPiece != null && targetPiece.Color != Color)
					return true;

				// En passant capture
				Position enPassantTarget = board.GetEnPassantTarget();
				if (enPassantTarget != null &&
					enPassantTarget.X == move.ToX &&
					enPassantTarget.Y == move.ToY)
					return true;
			}

			return false;
		}
	}
}

