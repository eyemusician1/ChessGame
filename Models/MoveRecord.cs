// Models/MoveRecord.cs
using System;

namespace ChessGame.Models
{
	/// <summary>
	/// Record of a move for undo functionality
	/// </summary>
	public class MoveRecord
	{
		public Move Move { get; }
		public Piece Piece { get; }
		public Piece CapturedPiece { get; set; }
		public Piece PromotedPiece { get; set; }
		public bool EnPassantCapture { get; set; }

		// Castling flags
		public bool WhiteKingMoved { get; }
		public bool BlackKingMoved { get; }
		public bool WhiteRookAMoved { get; }
		public bool WhiteRookHMoved { get; }
		public bool BlackRookAMoved { get; }
		public bool BlackRookHMoved { get; }

		// En passant target
		public Position EnPassantTarget { get; }

		public MoveRecord(
			Move move, Piece piece, Piece capturedPiece,
			bool whiteKingMoved, bool blackKingMoved,
			bool whiteRookAMoved, bool whiteRookHMoved,
			bool blackRookAMoved, bool blackRookHMoved,
			Position enPassantTarget)
		{
			Move = move;
			Piece = piece;
			CapturedPiece = capturedPiece;
			PromotedPiece = null;
			EnPassantCapture = false;

			WhiteKingMoved = whiteKingMoved;
			BlackKingMoved = blackKingMoved;
			WhiteRookAMoved = whiteRookAMoved;
			WhiteRookHMoved = whiteRookHMoved;
			BlackRookAMoved = blackRookAMoved;
			BlackRookHMoved = blackRookHMoved;

			EnPassantTarget = enPassantTarget;
		}
	}
}

