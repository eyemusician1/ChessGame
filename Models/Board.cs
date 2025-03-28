// Models/Board.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChessGame.Models
{
	/// <summary>
	/// Chess board class
	/// </summary>
	public class Board
	{
		private Piece[,] pieces;
		private Dictionary<Piece, Position> piecePositions;
		private Stack<MoveRecord> moveRecords;

		// For castling
		private bool whiteKingMoved;
		private bool blackKingMoved;
		private bool whiteRookAMoved;
		private bool whiteRookHMoved;
		private bool blackRookAMoved;
		private bool blackRookHMoved;

		// For en passant
		private Position enPassantTarget;

		public Board()
		{
			pieces = new Piece[8, 8];
			piecePositions = new Dictionary<Piece, Position>();
			moveRecords = new Stack<MoveRecord>();

			whiteKingMoved = false;
			blackKingMoved = false;
			whiteRookAMoved = false;
			whiteRookHMoved = false;
			blackRookAMoved = false;
			blackRookHMoved = false;

			enPassantTarget = null;
		}

		public void Initialize()
		{
			// Create new arrays and collections to ensure clean state
			pieces = new Piece[8, 8];
			piecePositions = new Dictionary<Piece, Position>();
			moveRecords = new Stack<MoveRecord>();

			// Reset castling flags
			whiteKingMoved = false;
			blackKingMoved = false;
			whiteRookAMoved = false;
			whiteRookHMoved = false;
			blackRookAMoved = false;
			blackRookHMoved = false;

			enPassantTarget = null;

			// Set up pawns - create new instances for each position
			for (int i = 0; i < 8; i++)
			{
				SetPiece(i, 1, new Pawn(PieceColor.White));
				SetPiece(i, 6, new Pawn(PieceColor.Black));
			}

			// Set up other pieces - create new instances for each position
			// White pieces
			SetPiece(0, 0, new Rook(PieceColor.White));
			SetPiece(1, 0, new Knight(PieceColor.White));
			SetPiece(2, 0, new Bishop(PieceColor.White));
			SetPiece(3, 0, new Queen(PieceColor.White));
			SetPiece(4, 0, new King(PieceColor.White));
			SetPiece(5, 0, new Bishop(PieceColor.White));
			SetPiece(6, 0, new Knight(PieceColor.White));
			SetPiece(7, 0, new Rook(PieceColor.White));

			// Black pieces
			SetPiece(0, 7, new Rook(PieceColor.Black));
			SetPiece(1, 7, new Knight(PieceColor.Black));
			SetPiece(2, 7, new Bishop(PieceColor.Black));
			SetPiece(3, 7, new Queen(PieceColor.Black));
			SetPiece(4, 7, new King(PieceColor.Black));
			SetPiece(5, 7, new Bishop(PieceColor.Black));
			SetPiece(6, 7, new Knight(PieceColor.Black));
			SetPiece(7, 7, new Rook(PieceColor.Black));
		}

		private void SetPiece(int x, int y, Piece piece)
		{
			// Remove any existing piece at this position
			if (pieces[x, y] != null && piecePositions.ContainsKey(pieces[x, y]))
			{
				piecePositions.Remove(pieces[x, y]);
			}

			// Set the new piece
			pieces[x, y] = piece;

			// Update the position dictionary
			if (piece != null)
			{
				// Remove any existing entry for this piece (should not happen, but just in case)
				if (piecePositions.ContainsKey(piece))
				{
					piecePositions.Remove(piece);
				}

				// Add the new position
				piecePositions[piece] = new Position(x, y);
			}
		}

		public void Display()
		{
			Console.WriteLine("  a b c d e f g h");
			Console.WriteLine("  ---------------");

			for (int y = 7; y >= 0; y--)
			{
				Console.Write($"{y + 1}|");

				for (int x = 0; x < 8; x++)
				{
					if (pieces[x, y] == null)
						Console.Write(" ");
					else
						Console.Write(pieces[x, y].Symbol);

					Console.Write(" ");
				}

				Console.WriteLine($"|{y + 1}");
			}

			Console.WriteLine("  ---------------");
			Console.WriteLine("  a b c d e f g h");
		}

		public bool IsValidMove(Move move)
		{
			// Check if coordinates are valid
			if (move.FromX < 0 || move.FromX > 7 || move.FromY < 0 || move.FromY > 7 ||
				move.ToX < 0 || move.ToX > 7 || move.ToY < 0 || move.ToY > 7)
				return false;

			// Get the piece at the starting position
			Piece piece = pieces[move.FromX, move.FromY];

			// Check if there is a piece at the starting position
			if (piece == null)
				return false;

			// Check if the piece belongs to the current player
			if (piece.Color != move.Player.Color)
				return false;

			// Check for castling
			if (piece is King && Math.Abs(move.ToX - move.FromX) == 2)
			{
				return IsValidCastling(move);
			}

			// Check for en passant
			if (piece is Pawn && move.ToX != move.FromX && pieces[move.ToX, move.ToY] == null)
			{
				if (enPassantTarget != null &&
					enPassantTarget.X == move.ToX &&
					enPassantTarget.Y == move.ToY)
				{
					return true;
				}
				return false;
			}

			// Check if the move is valid for this piece
			if (!piece.IsValidMove(move, this))
				return false;

			// Check if the move would put or leave the player in check
			if (WouldBeInCheck(move, piece.Color))
				return false;

			return true;
		}

		private bool IsValidCastling(Move move)
		{
			Piece king = pieces[move.FromX, move.FromY];

			// Check if it's a king
			if (!(king is King))
				return false;

			// Check if the king has moved
			if (king.Color == PieceColor.White && whiteKingMoved)
				return false;
			if (king.Color == PieceColor.Black && blackKingMoved)
				return false;

			// Check if the king is in check
			if (IsInCheck(king.Color))
				return false;

			// Determine if it's kingside or queenside castling
			bool kingsideCastling = move.ToX > move.FromX;

			// Check if the rook has moved
			if (king.Color == PieceColor.White)
			{
				if (kingsideCastling && whiteRookHMoved)
					return false;
				if (!kingsideCastling && whiteRookAMoved)
					return false;
			}
			else
			{
				if (kingsideCastling && blackRookHMoved)
					return false;
				if (!kingsideCastling && blackRookAMoved)
					return false;
			}

			// Check if there are pieces between the king and rook
			int rookX = kingsideCastling ? 7 : 0;
			int y = king.Color == PieceColor.White ? 0 : 7;

			// Check if the rook is there
			Piece rook = pieces[rookX, y];
			if (rook == null || !(rook is Rook) || rook.Color != king.Color)
				return false;

			// Check if the squares between the king and rook are empty
			int start = Math.Min(move.FromX, rookX) + 1;
			int end = Math.Max(move.FromX, rookX);

			for (int x = start; x < end; x++)
			{
				if (pieces[x, y] != null)
					return false;
			}

			// Check if the king passes through or ends up in check
			int direction = kingsideCastling ? 1 : -1;

			// Check the square the king passes through
			Move intermediateMove = new Move(
				move.FromX, move.FromY,
				move.FromX + direction, move.FromY,
				move.Player);

			if (WouldBeInCheck(intermediateMove, king.Color))
				return false;

			return true;
		}

		public void MakeMove(Move move)
		{
			// Store the piece at the starting position
			Piece piece = pieces[move.FromX, move.FromY];
			Piece capturedPiece = pieces[move.ToX, move.ToY];

			// Create a move record for undo
			MoveRecord record = new MoveRecord(
				move, piece, capturedPiece,
				whiteKingMoved, blackKingMoved,
				whiteRookAMoved, whiteRookHMoved,
				blackRookAMoved, blackRookHMoved,
				enPassantTarget);

			moveRecords.Push(record);

			// Handle castling
			if (piece is King && Math.Abs(move.ToX - move.FromX) == 2)
			{
				// Kingside or queenside
				bool kingsideCastling = move.ToX > move.FromX;
				int rookFromX = kingsideCastling ? 7 : 0;
				int rookToX = kingsideCastling ? 5 : 3;
				int y = piece.Color == PieceColor.White ? 0 : 7;

				// Move the rook
				Piece rook = pieces[rookFromX, y];

				// Update the board array
				pieces[rookToX, y] = rook;
				pieces[rookFromX, y] = null;

				// Update the position dictionary
				if (rook != null)
				{
					if (piecePositions.ContainsKey(rook))
					{
						piecePositions.Remove(rook);
					}
					piecePositions[rook] = new Position(rookToX, y);
				}
			}

			// Handle en passant capture
			if (piece is Pawn && move.ToX != move.FromX && pieces[move.ToX, move.ToY] == null)
			{
				if (enPassantTarget != null &&
					enPassantTarget.X == move.ToX &&
					enPassantTarget.Y == move.ToY)
				{
					// Capture the pawn that just moved
					int capturedPawnY = piece.Color == PieceColor.White ? move.ToY - 1 : move.ToY + 1;
					capturedPiece = pieces[move.ToX, capturedPawnY];

					// Update the board array
					pieces[move.ToX, capturedPawnY] = null;

					// Update the position dictionary
					if (capturedPiece != null && piecePositions.ContainsKey(capturedPiece))
					{
						piecePositions.Remove(capturedPiece);
					}

					// Update the move record
					record.CapturedPiece = capturedPiece;
					record.EnPassantCapture = true;
				}
			}

			// Update castling flags
			if (piece is King)
			{
				if (piece.Color == PieceColor.White)
					whiteKingMoved = true;
				else
					blackKingMoved = true;
			}
			else if (piece is Rook)
			{
				if (piece.Color == PieceColor.White)
				{
					if (move.FromX == 0)
						whiteRookAMoved = true;
					else if (move.FromX == 7)
						whiteRookHMoved = true;
				}
				else
				{
					if (move.FromX == 0)
						blackRookAMoved = true;
					else if (move.FromX == 7)
						blackRookHMoved = true;
				}
			}

			// Reset en passant target
			enPassantTarget = null;

			// Set en passant target if pawn moves two squares
			if (piece is Pawn && Math.Abs(move.ToY - move.FromY) == 2)
			{
				int y = (move.FromY + move.ToY) / 2; // Middle square
				enPassantTarget = new Position(move.ToX, y);
			}

			// Update the board array
			pieces[move.ToX, move.ToY] = piece;
			pieces[move.FromX, move.FromY] = null;

			// Update the position dictionary
			if (piece != null)
			{
				if (piecePositions.ContainsKey(piece))
				{
					piecePositions.Remove(piece);
				}
				piecePositions[piece] = new Position(move.ToX, move.ToY);
			}

			if (capturedPiece != null && piecePositions.ContainsKey(capturedPiece))
			{
				piecePositions.Remove(capturedPiece);
			}

			// Handle pawn promotion
			if (piece is Pawn)
			{
				if ((piece.Color == PieceColor.White && move.ToY == 7) ||
					(piece.Color == PieceColor.Black && move.ToY == 0))
				{
					// Promote to queen by default
					Queen queen = new Queen(piece.Color);

					// Update the board array
					pieces[move.ToX, move.ToY] = queen;

					// Update the position dictionary
					if (piecePositions.ContainsKey(piece))
					{
						piecePositions.Remove(piece);
					}
					piecePositions[queen] = new Position(move.ToX, move.ToY);

					// Update the move record
					record.PromotedPiece = queen;
				}
			}
		}

		public void UndoMove(Move move)
		{
			if (moveRecords.Count == 0)
				return;

			MoveRecord record = moveRecords.Pop();

			// Restore the piece
			pieces[record.Move.FromX, record.Move.FromY] = record.Piece;

			// If there was a promotion, use the original piece
			if (record.PromotedPiece != null)
			{
				pieces[record.Move.ToX, record.Move.ToY] = record.CapturedPiece;

				// Update piece positions
				if (piecePositions.ContainsKey(record.PromotedPiece))
				{
					piecePositions.Remove(record.PromotedPiece);
				}

				if (record.Piece != null)
				{
					piecePositions[record.Piece] = new Position(record.Move.FromX, record.Move.FromY);
				}
			}
			else
			{
				pieces[record.Move.ToX, record.Move.ToY] = record.CapturedPiece;

				// Update piece positions
				if (record.Piece != null)
				{
					piecePositions[record.Piece] = new Position(record.Move.FromX, record.Move.FromY);
				}

				if (record.CapturedPiece != null)
				{
					piecePositions[record.CapturedPiece] = new Position(record.Move.ToX, record.Move.ToY);
				}
			}

			// Handle en passant capture
			if (record.EnPassantCapture)
			{
				int capturedPawnY = record.Piece.Color == PieceColor.White ?
								   record.Move.ToY - 1 : record.Move.ToY + 1;

				pieces[record.Move.ToX, capturedPawnY] = record.CapturedPiece;

				if (record.CapturedPiece != null)
				{
					piecePositions[record.CapturedPiece] = new Position(record.Move.ToX, capturedPawnY);
				}
			}

			// Handle castling
			if (record.Piece is King && Math.Abs(record.Move.ToX - record.Move.FromX) == 2)
			{
				// Kingside or queenside
				bool kingsideCastling = record.Move.ToX > record.Move.FromX;
				int rookFromX = kingsideCastling ? 5 : 3;
				int rookToX = kingsideCastling ? 7 : 0;
				int y = record.Piece.Color == PieceColor.White ? 0 : 7;

				// Move the rook back
				Piece rook = pieces[rookFromX, y];
				pieces[rookToX, y] = rook;
				pieces[rookFromX, y] = null;

				if (rook != null && piecePositions.ContainsKey(rook))
				{
					piecePositions[rook] = new Position(rookToX, y);
				}
			}

			// Restore castling flags
			whiteKingMoved = record.WhiteKingMoved;
			blackKingMoved = record.BlackKingMoved;
			whiteRookAMoved = record.WhiteRookAMoved;
			whiteRookHMoved = record.WhiteRookHMoved;
			blackRookAMoved = record.BlackRookAMoved;
			blackRookHMoved = record.BlackRookHMoved;

			// Restore en passant target
			enPassantTarget = record.EnPassantTarget;
		}

		public bool IsCheckmate(PieceColor color)
		{
			// Check if the king is in check
			if (!IsInCheck(color))
				return false;

			// Check if there are any valid moves
			return !HasValidMoves(color);
		}

		public bool IsStalemate()
		{
			// Check if either player has no valid moves but is not in check
			return (!HasValidMoves(PieceColor.White) && !IsInCheck(PieceColor.White)) ||
				   (!HasValidMoves(PieceColor.Black) && !IsInCheck(PieceColor.Black));
		}

		private bool HasValidMoves(PieceColor color)
		{
			// Check all pieces of the given color
			foreach (var entry in piecePositions)
			{
				Piece piece = entry.Key;
				Position pos = entry.Value;

				if (piece.Color == color)
				{
					// Check all possible destination squares
					for (int toX = 0; toX < 8; toX++)
					{
						for (int toY = 0; toY < 8; toY++)
						{
							Move move = new Move(pos.X, pos.Y, toX, toY, new DummyPlayer(color));

							if (IsValidMove(move))
								return true;
						}
					}
				}
			}

			return false;
		}

		public bool IsInCheck(PieceColor color)
		{
			// Find the king
			King king = null;
			Position kingPos = null;

			foreach (var entry in piecePositions)
			{
				if (entry.Key is King && entry.Key.Color == color)
				{
					king = (King)entry.Key;
					kingPos = entry.Value;
					break;
				}
			}

			if (king == null || kingPos == null)
				return false;

			// Check if any opponent piece can capture the king
			PieceColor opponentColor = color == PieceColor.White ? PieceColor.Black : PieceColor.White;

			foreach (var entry in piecePositions)
			{
				Piece piece = entry.Key;
				Position pos = entry.Value;

				if (piece.Color == opponentColor)
				{
					Move move = new Move(pos.X, pos.Y, kingPos.X, kingPos.Y, new DummyPlayer(opponentColor));

					// Check if the piece can move to the king's position
					// Ignore the check validation to avoid infinite recursion
					if (piece.IsValidMove(move, this))
						return true;
				}
			}

			return false;
		}

		private bool WouldBeInCheck(Move move, PieceColor color)
		{
			// Make a temporary move
			Piece piece = pieces[move.FromX, move.FromY];
			Piece capturedPiece = pieces[move.ToX, move.ToY];

			// Save original positions
			Position originalPiecePos = null;
			Position originalCapturedPos = null;

			if (piecePositions.ContainsKey(piece))
			{
				originalPiecePos = piecePositions[piece];
			}

			if (capturedPiece != null && piecePositions.ContainsKey(capturedPiece))
			{
				originalCapturedPos = piecePositions[capturedPiece];
			}

			// Make the move
			pieces[move.ToX, move.ToY] = piece;
			pieces[move.FromX, move.FromY] = null;

			// Update piece position temporarily
			if (piecePositions.ContainsKey(piece))
			{
				piecePositions[piece] = new Position(move.ToX, move.ToY);
			}

			// Remove captured piece temporarily
			if (capturedPiece != null && piecePositions.ContainsKey(capturedPiece))
			{
				piecePositions.Remove(capturedPiece);
			}

			// Check if the king is in check
			bool inCheck = IsInCheck(color);

			// Undo the temporary move
			pieces[move.FromX, move.FromY] = piece;
			pieces[move.ToX, move.ToY] = capturedPiece;

			// Restore original positions
			if (originalPiecePos != null && piecePositions.ContainsKey(piece))
			{
				piecePositions[piece] = originalPiecePos;
			}

			if (capturedPiece != null)
			{
				if (originalCapturedPos != null)
				{
					piecePositions[capturedPiece] = originalCapturedPos;
				}
				else
				{
					piecePositions.Add(capturedPiece, new Position(move.ToX, move.ToY));
				}
			}

			return inCheck;
		}

		public Piece GetPiece(int x, int y)
		{
			if (x < 0 || x > 7 || y < 0 || y > 7)
				return null;

			return pieces[x, y];
		}

		public Position GetEnPassantTarget()
		{
			return enPassantTarget;
		}

		public Dictionary<Piece, Position> GetPiecesByColor(PieceColor color)
		{
			return piecePositions
				.Where(kvp => kvp.Key.Color == color)
				.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
		}

		// Method for board evaluation (used by AI)
		public int Evaluate(PieceColor maximizingColor)
		{
			int score = 0;

			// Material value
			foreach (var entry in piecePositions)
			{
				Piece piece = entry.Key;
				int value = GetPieceValue(piece);

				if (piece.Color == maximizingColor)
					score += value;
				else
					score -= value;
			}

			// Position value
			foreach (var entry in piecePositions)
			{
				Piece piece = entry.Key;
				Position pos = entry.Value;
				int positionValue = GetPositionValue(piece, pos);

				if (piece.Color == maximizingColor)
					score += positionValue;
				else
					score -= positionValue;
			}

			// Check and checkmate
			if (IsCheckmate(maximizingColor))
				score -= 10000; // Huge penalty for being checkmated
			else if (IsCheckmate(maximizingColor == PieceColor.White ? PieceColor.Black : PieceColor.White))
				score += 10000; // Huge bonus for checkmating opponent
			else if (IsInCheck(maximizingColor))
				score -= 50; // Penalty for being in check
			else if (IsInCheck(maximizingColor == PieceColor.White ? PieceColor.Black : PieceColor.White))
				score += 50; // Bonus for putting opponent in check

			return score;
		}

		private int GetPieceValue(Piece piece)
		{
			if (piece is Pawn) return 100;
			if (piece is Knight) return 320;
			if (piece is Bishop) return 330;
			if (piece is Rook) return 500;
			if (piece is Queen) return 900;
			if (piece is King) return 20000;

			return 0;
		}

		private int GetPositionValue(Piece piece, Position pos)
		{
			// Position tables for each piece type
			// These are simplified examples - real chess engines use more sophisticated tables

			// Pawns prefer to advance and control the center
			int[,] pawnTable = {
			{0,  0,  0,  0,  0,  0,  0,  0},
			{50, 50, 50, 50, 50, 50, 50, 50},
			{10, 10, 20, 30, 30, 20, 10, 10},
			{5,  5, 10, 25, 25, 10,  5,  5},
			{0,  0,  0, 20, 20,  0,  0,  0},
			{5, -5,-10,  0,  0,-10, -5,  5},
			{5, 10, 10,-20,-20, 10, 10,  5},
			{0,  0,  0,  0,  0,  0,  0,  0}
		};

			// Knights prefer the center and avoid edges
			int[,] knightTable = {
			{-50,-40,-30,-30,-30,-30,-40,-50},
			{-40,-20,  0,  0,  0,  0,-20,-40},
			{-30,  0, 10, 15, 15, 10,  0,-30},
			{-30,  5, 15, 20, 20, 15,  5,-30},
			{-30,  0, 15, 20, 20, 15,  0,-30},
			{-30,  5, 10, 15, 15, 10,  5,-30},
			{-40,-20,  0,  5,  5,  0,-20,-40},
			{-50,-40,-30,-30,-30,-30,-40,-50}
		};

			// Bishops prefer diagonals
			int[,] bishopTable = {
			{-20,-10,-10,-10,-10,-10,-10,-20},
			{-10,  0,  0,  0,  0,  0,  0,-10},
			{-10,  0, 10, 10, 10, 10,  0,-10},
			{-10,  5,  5, 10, 10,  5,  5,-10},
			{-10,  0,  5, 10, 10,  5,  0,-10},
			{-10,  5,  5,  5,  5,  5,  5,-10},
			{-10,  0,  5,  0,  0,  5,  0,-10},
			{-20,-10,-10,-10,-10,-10,-10,-20}
		};

			// Rooks prefer open files and 7th rank
			int[,] rookTable = {
			{0,  0,  0,  0,  0,  0,  0,  0},
			{5, 10, 10, 10, 10, 10, 10,  5},
			{-5,  0,  0,  0,  0,  0,  0, -5},
			{-5,  0,  0,  0,  0,  0,  0, -5},
			{-5,  0,  0,  0,  0,  0,  0, -5},
			{-5,  0,  0,  0,  0,  0,  0, -5},
			{-5,  0,  0,  0,  0,  0,  0, -5},
			{0,  0,  0,  5,  5,  0,  0,  0}
		};

			// Queens combine rook and bishop mobility
			int[,] queenTable = {
			{-20,-10,-10, -5, -5,-10,-10,-20},
			{-10,  0,  0,  0,  0,  0,  0,-10},
			{-10,  0,  5,  5,  5,  5,  0,-10},
			{-5,  0,  5,  5,  5,  5,  0, -5},
			{0,  0,  5,  5,  5,  5,  0, -5},
			{-10,  5,  5,  5,  5,  5,  0,-10},
			{-10,  0,  5,  0,  0,  0,  0,-10},
			{-20,-10,-10, -5, -5,-10,-10,-20}
		};

			// Kings prefer safety in early/mid game
			int[,] kingMidGameTable = {
			{-30,-40,-40,-50,-50,-40,-40,-30},
			{-30,-40,-40,-50,-50,-40,-40,-30},
			{-30,-40,-40,-50,-50,-40,-40,-30},
			{-30,-40,-40,-50,-50,-40,-40,-30},
			{-20,-30,-30,-40,-40,-30,-30,-20},
			{-10,-20,-20,-20,-20,-20,-20,-10},
			{20, 20,  0,  0,  0,  0, 20, 20},
			{20, 30, 10,  0,  0, 10, 30, 20}
		};

			// Flip the tables for black pieces
			int x = pos.X;
			int y = pos.Y;

			if (piece.Color == PieceColor.Black)
				y = 7 - y;

			if (piece is Pawn)
				return pawnTable[y, x];
			else if (piece is Knight)
				return knightTable[y, x];
			else if (piece is Bishop)
				return bishopTable[y, x];
			else if (piece is Rook)
				return rookTable[y, x];
			else if (piece is Queen)
				return queenTable[y, x];
			else if (piece is King)
				return kingMidGameTable[y, x];

			return 0;
		}
	}
}

