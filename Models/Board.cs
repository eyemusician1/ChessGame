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

		// Cache Kings for faster check detection
		private King whiteKing;
		private King blackKing;
		private Position whiteKingPosition;
		private Position blackKingPosition;

		// For castling
		private bool whiteKingMoved;
		private bool blackKingMoved;
		private bool whiteRookAMoved;
		private bool whiteRookHMoved;
		private bool blackRookAMoved;
		private bool blackRookHMoved;

		// For en passant
		private Position enPassantTarget;

		// For optimization: evaluation caching
		private Dictionary<string, int> evaluationCache;
		private const int EVALUATION_CACHE_SIZE = 1000;

		public Board()
		{
			pieces = new Piece[8, 8];
			piecePositions = new Dictionary<Piece, Position>();
			moveRecords = new Stack<MoveRecord>();
			evaluationCache = new Dictionary<string, int>(EVALUATION_CACHE_SIZE);

			whiteKingMoved = false;
			blackKingMoved = false;
			whiteRookAMoved = false;
			whiteRookHMoved = false;
			blackRookAMoved = false;
			blackRookHMoved = false;

			enPassantTarget = null;
			whiteKing = null;
			blackKing = null;
			whiteKingPosition = null;
			blackKingPosition = null;
		}

		public void Initialize()
		{
			// Create new arrays and collections to ensure clean state
			pieces = new Piece[8, 8];
			piecePositions = new Dictionary<Piece, Position>();
			moveRecords = new Stack<MoveRecord>();
			evaluationCache.Clear();

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
				SetPieceDirectly(i, 1, new Pawn(PieceColor.White));
				SetPieceDirectly(i, 6, new Pawn(PieceColor.Black));
			}

			// Set up other pieces - create new instances for each position
			// White pieces
			SetPieceDirectly(0, 0, new Rook(PieceColor.White));
			SetPieceDirectly(1, 0, new Knight(PieceColor.White));
			SetPieceDirectly(2, 0, new Bishop(PieceColor.White));
			SetPieceDirectly(3, 0, new Queen(PieceColor.White));
			whiteKing = new King(PieceColor.White);
			SetPieceDirectly(4, 0, whiteKing);
			whiteKingPosition = new Position(4, 0);
			SetPieceDirectly(5, 0, new Bishop(PieceColor.White));
			SetPieceDirectly(6, 0, new Knight(PieceColor.White));
			SetPieceDirectly(7, 0, new Rook(PieceColor.White));

			// Black pieces
			SetPieceDirectly(0, 7, new Rook(PieceColor.Black));
			SetPieceDirectly(1, 7, new Knight(PieceColor.Black));
			SetPieceDirectly(2, 7, new Bishop(PieceColor.Black));
			SetPieceDirectly(3, 7, new Queen(PieceColor.Black));
			blackKing = new King(PieceColor.Black);
			SetPieceDirectly(4, 7, blackKing);
			blackKingPosition = new Position(4, 7);
			SetPieceDirectly(5, 7, new Bishop(PieceColor.Black));
			SetPieceDirectly(6, 7, new Knight(PieceColor.Black));
			SetPieceDirectly(7, 7, new Rook(PieceColor.Black));
		}

		// Optimized method for initial board setup - faster than using SetPiece
		private void SetPieceDirectly(int x, int y, Piece piece)
		{
			pieces[x, y] = piece;
			if (piece != null)
			{
				piecePositions[piece] = new Position(x, y);
			}
		}

		public void SetPiece(int x, int y, Piece piece)
		{
			// Clear evaluation cache since board state is changing
			evaluationCache.Clear();

			// Remove any existing piece at this position from the dictionary
			if (pieces[x, y] != null)
			{
				Piece existingPiece = pieces[x, y];
				if (piecePositions.ContainsKey(existingPiece))
				{
					// If the existing piece is a king, ensure we maintain king tracking
					if (existingPiece is King)
					{
						if (existingPiece.Color == PieceColor.White)
						{
							whiteKing = null;
							whiteKingPosition = null;
						}
						else
						{
							blackKing = null;
							blackKingPosition = null;
						}
					}
					piecePositions.Remove(existingPiece);
				}
			}

			// Set the new piece on the board
			pieces[x, y] = piece;

			// Update the position dictionary
			if (piece != null)
			{
				// If this piece already exists elsewhere on the board, remove it first
				if (piecePositions.ContainsKey(piece))
				{
					Position oldPos = piecePositions[piece];
					// Only clear the old position if it actually contains this piece
					if (pieces[oldPos.X, oldPos.Y] == piece)
					{
						pieces[oldPos.X, oldPos.Y] = null;
					}
					piecePositions.Remove(piece);
				}

				// Add new position entry
				piecePositions[piece] = new Position(x, y);

				// Update king position cache if needed
				if (piece is King)
				{
					if (piece.Color == PieceColor.White)
					{
						whiteKing = (King)piece;
						whiteKingPosition = new Position(x, y);
					}
					else
					{
						blackKing = (King)piece;
						blackKingPosition = new Position(x, y);
					}
				}
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

			// Check each square the king passes through
			for (int offset = 0; offset <= 2; offset++)  // Check current, first step, and destination
			{
				int checkX = move.FromX + (direction * offset);
				if (checkX != move.FromX)  // Don't check the starting square for attacks
				{
					PieceColor opponentColor = king.Color == PieceColor.White ? PieceColor.Black : PieceColor.White;
					var opponentPieces = GetPiecesByColor(opponentColor);

					foreach (var entry in opponentPieces)
					{
						Piece piece = entry.Key;
						Position pos = entry.Value;
						Move checkMove = new Move(pos.X, pos.Y, checkX, y, new DummyPlayer(opponentColor));

						// Skip the king check validation to avoid recursion
						if (piece.IsValidMove(checkMove, this))
							return false;
					}
				}
			}

			return true;
		}

		public void MakeMove(Move move)
		{
			// Clear evaluation cache since board state is changing
			evaluationCache.Clear();

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

				// Use SetPiece to properly update the piece tracking
				SetPiece(rookToX, y, rook);
				SetPiece(rookFromX, y, null);
			}

			// Handle en passant capture
			if (piece is Pawn && move.ToX != move.FromX && pieces[move.ToX, move.ToY] == null)
			{
				if (enPassantTarget != null &&
					enPassantTarget.X == move.ToX &&
					enPassantTarget.Y == move.ToY)
				{
					// For en passant, we need to capture the pawn that just moved two squares
					// This pawn is located on the same file as the destination square (move.ToX)
					// but on the same rank as the en passant target (enPassantTarget.Y)
					int capturedPawnY = piece.Color == PieceColor.White ? move.ToY + 1 : move.ToY - 1;

					// The captured pawn is on the same file as where we're moving, but one square forward/backward 
					// depending on which color is capturing
					capturedPiece = pieces[move.ToX, capturedPawnY];

					// Update the move record
					record.CapturedPiece = capturedPiece;
					record.EnPassantCapture = true;

					// Remove the captured pawn
					SetPiece(move.ToX, capturedPawnY, null);
				}
			}

			// Update castling flags
			if (piece is King)
			{
				if (piece.Color == PieceColor.White)
				{
					whiteKingMoved = true;
					whiteKingPosition = new Position(move.ToX, move.ToY);
				}
				else
				{
					blackKingMoved = true;
					blackKingPosition = new Position(move.ToX, move.ToY);
				}
			}
			else if (piece is Rook)
			{
				if (piece.Color == PieceColor.White)
				{
					if (move.FromX == 0 && move.FromY == 0)
						whiteRookAMoved = true;
					else if (move.FromX == 7 && move.FromY == 0)
						whiteRookHMoved = true;
				}
				else
				{
					if (move.FromX == 0 && move.FromY == 7)
						blackRookAMoved = true;
					else if (move.FromX == 7 && move.FromY == 7)
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

			// Use SetPiece to properly update the piece tracking
			SetPiece(move.ToX, move.ToY, piece);
			SetPiece(move.FromX, move.FromY, null);

			// Handle pawn promotion
			if (piece is Pawn)
			{
				if ((piece.Color == PieceColor.White && move.ToY == 7) ||
					(piece.Color == PieceColor.Black && move.ToY == 0))
				{
					// Promote to queen by default
					Queen queen = new Queen(piece.Color);

					// Update the move record
					record.PromotedPiece = queen;

					// Use SetPiece to properly update the piece tracking
					SetPiece(move.ToX, move.ToY, queen);
				}
			}

			// Validate the board state after the move
			ValidateBoardState();
		}

		public void UndoMove()
		{
			if (moveRecords.Count == 0)
				return;

			// Clear evaluation cache since board state is changing
			evaluationCache.Clear();

			MoveRecord record = moveRecords.Pop();
			Move move = record.Move;

			// Handle promoted pieces
			if (record.PromotedPiece != null)
			{
				// Remove the promoted piece
				if (piecePositions.ContainsKey(record.PromotedPiece))
				{
					piecePositions.Remove(record.PromotedPiece);
				}
				pieces[move.ToX, move.ToY] = null;
			}

			// Restore the original piece
			SetPiece(move.FromX, move.FromY, record.Piece);

			// If it wasn't a promotion, clear the destination square
			if (record.PromotedPiece == null)
			{
				pieces[move.ToX, move.ToY] = null;

				// If the piece position is still pointing to the new position, update it
				if (piecePositions.ContainsKey(record.Piece) &&
					piecePositions[record.Piece].X == move.ToX &&
					piecePositions[record.Piece].Y == move.ToY)
				{
					piecePositions[record.Piece] = new Position(move.FromX, move.FromY);
				}
			}

			// Restore any captured piece
			if (record.CapturedPiece != null)
			{
				if (record.EnPassantCapture)
				{
					int capturedPawnY = record.Piece.Color == PieceColor.White ?
									   move.ToY - 1 : move.ToY + 1;
					SetPiece(move.ToX, capturedPawnY, record.CapturedPiece);
				}
				else
				{
					SetPiece(move.ToX, move.ToY, record.CapturedPiece);
				}
			}

			// Handle castling
			if (record.Piece is King && Math.Abs(move.ToX - move.FromX) == 2)
			{
				// Kingside or queenside
				bool kingsideCastling = move.ToX > move.FromX;
				int rookFromX = kingsideCastling ? 5 : 3;
				int rookToX = kingsideCastling ? 7 : 0;
				int y = record.Piece.Color == PieceColor.White ? 0 : 7;

				// Move the rook back
				Piece rook = pieces[rookFromX, y];
				SetPiece(rookToX, y, rook);
				SetPiece(rookFromX, y, null);
			}

			// Update king position cache
			if (record.Piece is King)
			{
				if (record.Piece.Color == PieceColor.White)
				{
					whiteKingPosition = new Position(move.FromX, move.FromY);
					whiteKing = (King)record.Piece;
				}
				else
				{
					blackKingPosition = new Position(move.FromX, move.FromY);
					blackKing = (King)record.Piece;
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

			// Verify board consistency
			ValidateBoardState();
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
			// Optimization: Create a copy of the piece positions to avoid modification during enumeration
			var pieceList = GetPiecesByColor(color);

			// Check all pieces of the given color
			foreach (var entry in pieceList)
			{
				Piece piece = entry.Key;
				Position pos = entry.Value;

				// For each piece, only check reasonable destination squares based on piece type
				List<Position> potentialMoves = GetPotentialMoves(piece, pos);

				foreach (var destination in potentialMoves)
				{
					Move move = new Move(pos.X, pos.Y, destination.X, destination.Y, new DummyPlayer(color));

					if (IsValidMove(move))
						return true;
				}
			}

			return false;
		}

		// Optimization: Only generate reasonable moves for each piece type
		private List<Position> GetPotentialMoves(Piece piece, Position pos)
		{
			List<Position> moves = new List<Position>();

			// For knights: Check the 8 L-shaped moves
			if (piece is Knight)
			{
				int[] dx = { 1, 2, 2, 1, -1, -2, -2, -1 };
				int[] dy = { 2, 1, -1, -2, -2, -1, 1, 2 };

				for (int i = 0; i < 8; i++)
				{
					int nx = pos.X + dx[i];
					int ny = pos.Y + dy[i];
					if (nx >= 0 && nx < 8 && ny >= 0 && ny < 8)
						moves.Add(new Position(nx, ny));
				}
				return moves;
			}

			// For kings: Check all 8 surrounding squares + castling
			if (piece is King)
			{
				for (int dx = -1; dx <= 1; dx++)
				{
					for (int dy = -1; dy <= 1; dy++)
					{
						if (dx == 0 && dy == 0) continue;

						int nx = pos.X + dx;
						int ny = pos.Y + dy;
						if (nx >= 0 && nx < 8 && ny >= 0 && ny < 8)
							moves.Add(new Position(nx, ny));
					}
				}

				// Add castling moves if king hasn't moved
				if ((piece.Color == PieceColor.White && !whiteKingMoved) ||
					(piece.Color == PieceColor.Black && !blackKingMoved))
				{
					moves.Add(new Position(pos.X + 2, pos.Y)); // Kingside
					moves.Add(new Position(pos.X - 2, pos.Y)); // Queenside
				}

				return moves;
			}

			// For pawns: Forward moves + captures
			if (piece is Pawn)
			{
				int direction = piece.Color == PieceColor.White ? 1 : -1;
				int startRank = piece.Color == PieceColor.White ? 1 : 6;

				// Forward one square
				int ny = pos.Y + direction;
				if (ny >= 0 && ny < 8 && pieces[pos.X, ny] == null)
				{
					moves.Add(new Position(pos.X, ny));

					// Forward two squares from starting position
					if (pos.Y == startRank)
					{
						ny = pos.Y + 2 * direction;
						if (pieces[pos.X, ny] == null)
							moves.Add(new Position(pos.X, ny));
					}
				}

				// Captures (including en passant)
				for (int dx = -1; dx <= 1; dx += 2)
				{
					int nx = pos.X + dx;
					ny = pos.Y + direction;

					if (nx >= 0 && nx < 8 && ny >= 0 && ny < 8)
					{
						// Regular capture
						if (pieces[nx, ny] != null && pieces[nx, ny].Color != piece.Color)
							moves.Add(new Position(nx, ny));

						// En passant
						if (pieces[nx, ny] == null && enPassantTarget != null &&
							nx == enPassantTarget.X && ny == enPassantTarget.Y)
							moves.Add(new Position(nx, ny));
					}
				}

				return moves;
			}

			// For sliding pieces (Queen, Rook, Bishop): Check all possible directions
			int[][] directions;

			if (piece is Queen)
				directions = new int[][] { new int[] {1, 0}, new int[] {0, 1}, new int[] {-1, 0}, new int[] {0, -1},
										 new int[] {1, 1}, new int[] {1, -1}, new int[] {-1, 1}, new int[] {-1, -1} };
			else if (piece is Rook)
				directions = new int[][] { new int[] { 1, 0 }, new int[] { 0, 1 }, new int[] { -1, 0 }, new int[] { 0, -1 } };
			else if (piece is Bishop)
				directions = new int[][] { new int[] { 1, 1 }, new int[] { 1, -1 }, new int[] { -1, 1 }, new int[] { -1, -1 } };
			else
				directions = new int[][] { }; // Shouldn't happen

			foreach (var dir in directions)
			{
				int dx = dir[0];
				int dy = dir[1];

				for (int dist = 1; dist < 8; dist++)
				{
					int nx = pos.X + dx * dist;
					int ny = pos.Y + dy * dist;

					if (nx < 0 || nx >= 8 || ny < 0 || ny >= 8)
						break;

					moves.Add(new Position(nx, ny));

					// Stop if we hit a piece
					if (pieces[nx, ny] != null)
						break;
				}
			}

			return moves;
		}

		public bool IsInCheck(PieceColor color)
		{
			// Use the cached king positions for efficiency
			Position kingPos = color == PieceColor.White ? whiteKingPosition : blackKingPosition;

			if (kingPos == null)
			{
				// Fallback to search if cache is not available
				foreach (var entry in piecePositions)
				{
					if (entry.Key is King && entry.Key.Color == color)
					{
						kingPos = entry.Value;

						// Update the cache
						if (color == PieceColor.White)
						{
							whiteKing = (King)entry.Key;
							whiteKingPosition = kingPos;
						}
						else
						{
							blackKing = (King)entry.Key;
							blackKingPosition = kingPos;
						}
						break;
					}
				}
			}

			if (kingPos == null)
				return false;

			// Check if any opponent piece can capture the king
			PieceColor opponentColor = color == PieceColor.White ? PieceColor.Black : PieceColor.White;
			var opponentPieces = GetPiecesByColor(opponentColor);

			foreach (var entry in opponentPieces)
			{
				Piece piece = entry.Key;
				Position pos = entry.Value;

				// Optimization: Quick check based on piece type to see if it's even possible
				if (!CanPotentiallyAttack(piece, pos, kingPos))
					continue;

				Move move = new Move(pos.X, pos.Y, kingPos.X, kingPos.Y, new DummyPlayer(opponentColor));

				// Check if the piece can move to the king's position
				// Ignore the check validation to avoid infinite recursion
				if (piece.IsValidMove(move, this))
					return true;
			}

			return false;
		}

		// Quick check to see if a piece could potentially attack a target square before doing full validation
		private bool CanPotentiallyAttack(Piece piece, Position from, Position to)
		{
			int dx = to.X - from.X;
			int dy = to.Y - from.Y;
			int absDx = Math.Abs(dx);
			int absDy = Math.Abs(dy);

			// Knights have a specific attack pattern
			if (piece is Knight)
				return (absDx == 1 && absDy == 2) || (absDx == 2 && absDy == 1);

			// Kings can only attack adjacent squares
			if (piece is King)
				return absDx <= 1 && absDy <= 1;

			// Pawns have specific attack patterns
			if (piece is Pawn)
			{
				int direction = piece.Color == PieceColor.White ? 1 : -1;

				// Pawns attack diagonally forward
				if (absDx == 1 && dy == direction)
					return true;

				// Check for en passant potential
				if (absDx == 1 && dy == direction && enPassantTarget != null &&
					to.X == enPassantTarget.X && to.Y == enPassantTarget.Y)
					return true;

				return false;
			}

			// For sliding pieces, make sure they're moving in a valid direction
			bool validDirection = false;

			if (piece is Rook)
				validDirection = dx == 0 || dy == 0;
			else if (piece is Bishop)
				validDirection = absDx == absDy;
			else if (piece is Queen)
				validDirection = dx == 0 || dy == 0 || absDx == absDy;

			if (!validDirection)
				return false;

			// For sliding pieces, check if there are any pieces in between
			int stepX = dx == 0 ? 0 : dx > 0 ? 1 : -1;
			int stepY = dy == 0 ? 0 : dy > 0 ? 1 : -1;

			int currentX = from.X + stepX;
			int currentY = from.Y + stepY;

			while (currentX != to.X || currentY != to.Y)
			{
				if (pieces[currentX, currentY] != null)
					return false;  // Piece in the way

				currentX += stepX;
				currentY += stepY;
			}

			return true;
		}

		// Add a new method to validate board state consistency
		private void ValidateBoardState()
		{
			// Check that piece positions dictionary is consistent with the board array
			bool inconsistencyFound = false;

			// Check each position in pieces array matches dictionary
			for (int x = 0; x < 8; x++)
			{
				for (int y = 0; y < 8; y++)
				{
					Piece p = pieces[x, y];
					if (p != null)
					{
						if (!piecePositions.ContainsKey(p))
						{
							// Piece on board but not in dictionary
							inconsistencyFound = true;
							piecePositions[p] = new Position(x, y);
						}
						else if (piecePositions[p].X != x || piecePositions[p].Y != y)
						{
							// Position mismatch between board and dictionary
							inconsistencyFound = true;
							piecePositions[p] = new Position(x, y);
						}
					}
				}
			}

			// Check each entry in the dictionary matches the board
			foreach (var entry in piecePositions.ToList())
			{
				Piece p = entry.Key;
				Position pos = entry.Value;

				if (pos.X < 0 || pos.X > 7 || pos.Y < 0 || pos.Y > 7 ||
					pieces[pos.X, pos.Y] != p)
				{
					// Dictionary entry doesn't match board
					inconsistencyFound = true;
					piecePositions.Remove(p);
				}
			}

			// Verify king position caches
			if (whiteKing != null)
			{
				if (!piecePositions.ContainsKey(whiteKing) ||
					(whiteKingPosition.X != piecePositions[whiteKing].X ||
					 whiteKingPosition.Y != piecePositions[whiteKing].Y))
				{
					// King position cache is inconsistent
					inconsistencyFound = true;
					if (piecePositions.ContainsKey(whiteKing))
					{
						whiteKingPosition = piecePositions[whiteKing];
					}
					else
					{
						// White king is missing, try to find it
						whiteKing = null;
						whiteKingPosition = null;

						for (int x = 0; x < 8; x++)
						{
							for (int y = 0; y < 8; y++)
							{
								if (pieces[x, y] is King && pieces[x, y].Color == PieceColor.White)
								{
									whiteKing = (King)pieces[x, y];
									whiteKingPosition = new Position(x, y);
									piecePositions[whiteKing] = whiteKingPosition;
									break;
								}
							}
							if (whiteKing != null) break;
						}
					}
				}
			}

			if (blackKing != null)
			{
				if (!piecePositions.ContainsKey(blackKing) ||
					(blackKingPosition.X != piecePositions[blackKing].X ||
					 blackKingPosition.Y != piecePositions[blackKing].Y))
				{
					// King position cache is inconsistent
					inconsistencyFound = true;
					if (piecePositions.ContainsKey(blackKing))
					{
						blackKingPosition = piecePositions[blackKing];
					}
					else
					{
						// Black king is missing, try to find it
						blackKing = null;
						blackKingPosition = null;

						for (int x = 0; x < 8; x++)
						{
							for (int y = 0; y < 8; y++)
							{
								if (pieces[x, y] is King && pieces[x, y].Color == PieceColor.Black)
								{
									blackKing = (King)pieces[x, y];
									blackKingPosition = new Position(x, y);
									piecePositions[blackKing] = blackKingPosition;
									break;
								}
							}
							if (blackKing != null) break;
						}
					}
				}
			}

			// If inconsistencies were found and fixed, clear the evaluation cache
			if (inconsistencyFound)
			{
				evaluationCache.Clear();
			}
		}

		private bool WouldBeInCheck(Move move, PieceColor color)
		{
			// Create a copy of the board and make the move on it
			Board tempBoard = new Board();
			CopyBoardState(tempBoard);

			// Make the move on the temporary board
			Piece piece = tempBoard.GetPiece(move.FromX, move.FromY);

			// Handle en passant special case
			bool isEnPassant = false;
			if (piece is Pawn && move.ToX != move.FromX && tempBoard.GetPiece(move.ToX, move.ToY) == null)
			{
				Position enPassantTarget = tempBoard.GetEnPassantTarget();
				if (enPassantTarget != null && enPassantTarget.X == move.ToX && enPassantTarget.Y == move.ToY)
				{
					isEnPassant = true;
				}
			}

			// Handle castling special case
			bool isCastling = piece is King && Math.Abs(move.ToX - move.FromX) == 2;

			if (isEnPassant || isCastling)
			{
				// For special moves, we need to actually make the move on the temp board
				tempBoard.MakeMove(move);
			}
			else
			{
				// For normal moves, we can just update the piece positions
				tempBoard.SetPiece(move.ToX, move.ToY, piece);
				tempBoard.SetPiece(move.FromX, move.FromY, null);

				// Update king position if the king is moving
				if (piece is King && piece.Color == color)
				{
					if (color == PieceColor.White)
					{
						var field = typeof(Board).GetField("whiteKingPosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
						field.SetValue(tempBoard, new Position(move.ToX, move.ToY));
					}
					else
					{
						var field = typeof(Board).GetField("blackKingPosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
						field.SetValue(tempBoard, new Position(move.ToX, move.ToY));
					}
				}
			}
			// Check if the king is in check on the temporary board
			return tempBoard.IsInCheck(color);
		}

		// Helper method to copy the board state to a temporary board
		private void CopyBoardState(Board targetBoard)
		{
			// Copy pieces
			for (int x = 0; x < 8; x++)
			{
				for (int y = 0; y < 8; y++)
				{
					if (pieces[x, y] != null)
					{
						Piece originalPiece = pieces[x, y];
						Piece newPiece;

						// Create a new piece of the same type and color
						if (originalPiece is Pawn)
							newPiece = new Pawn(originalPiece.Color);
						else if (originalPiece is Knight)
							newPiece = new Knight(originalPiece.Color);
						else if (originalPiece is Bishop)
							newPiece = new Bishop(originalPiece.Color);
						else if (originalPiece is Rook)
							newPiece = new Rook(originalPiece.Color);
						else if (originalPiece is Queen)
							newPiece = new Queen(originalPiece.Color);
						else if (originalPiece is King)
							newPiece = new King(originalPiece.Color);
						else
							continue;  // Skip if unknown piece type

						targetBoard.SetPiece(x, y, newPiece);
					}
				}
			}

			// Copy other relevant state
			// Castling flags
			typeof(Board).GetField("whiteKingMoved", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
				.SetValue(targetBoard, whiteKingMoved);
			typeof(Board).GetField("blackKingMoved", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
				.SetValue(targetBoard, blackKingMoved);
			typeof(Board).GetField("whiteRookAMoved", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
				.SetValue(targetBoard, whiteRookAMoved);
			typeof(Board).GetField("whiteRookHMoved", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
				.SetValue(targetBoard, whiteRookHMoved);
			typeof(Board).GetField("blackRookAMoved", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
				.SetValue(targetBoard, blackRookAMoved);
			typeof(Board).GetField("blackRookHMoved", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
				.SetValue(targetBoard, blackRookHMoved);

			// En passant
			if (enPassantTarget != null)
			{
				Position newTarget = new Position(enPassantTarget.X, enPassantTarget.Y);
				typeof(Board).GetField("enPassantTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
					.SetValue(targetBoard, newTarget);
			}
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
			// Check if we have this position's evaluation in cache
			string boardKey = GetBoardStateKey(maximizingColor);
			if (evaluationCache.ContainsKey(boardKey))
			{
				return evaluationCache[boardKey];
			}

			int score = 0;

			// Check for checkmate or stalemate first (highest priority)
			if (IsCheckmate(maximizingColor))
				return -10000; // Huge penalty for being checkmated
			else if (IsCheckmate(maximizingColor == PieceColor.White ? PieceColor.Black : PieceColor.White))
				return 10000; // Huge bonus for checkmating opponent
			else if (IsStalemate())
				return 0; // Stalemate is a draw

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

			// Check and mobility bonuses
			if (IsInCheck(maximizingColor))
				score -= 50; // Penalty for being in check
			else if (IsInCheck(maximizingColor == PieceColor.White ? PieceColor.Black : PieceColor.White))
				score += 50; // Bonus for putting opponent in check

			// Control of center
			score += CalculateCenterControl(maximizingColor);

			// Development in early game (first 10 moves)
			if (moveRecords.Count < 20)
			{
				score += CalculateDevelopment(maximizingColor);
			}

			// Cache this evaluation result
			if (evaluationCache.Count >= EVALUATION_CACHE_SIZE)
			{
				// If cache is full, clear a portion of it
				var keysToRemove = evaluationCache.Keys.Take(EVALUATION_CACHE_SIZE / 4).ToList();
				foreach (var key in keysToRemove)
				{
					evaluationCache.Remove(key);
				}
			}

			evaluationCache[boardKey] = score;
			return score;
		}

		// Generate a unique key for the current board state for caching
		private string GetBoardStateKey(PieceColor perspective)
		{
			StringBuilder sb = new StringBuilder();

			// Add the current perspective
			sb.Append(perspective == PieceColor.White ? 'W' : 'B');

			// Add castling rights
			sb.Append(whiteKingMoved ? '0' : '1');
			sb.Append(blackKingMoved ? '0' : '1');
			sb.Append(whiteRookAMoved ? '0' : '1');
			sb.Append(whiteRookHMoved ? '0' : '1');
			sb.Append(blackRookAMoved ? '0' : '1');
			sb.Append(blackRookHMoved ? '0' : '1');

			// Add en passant target
			if (enPassantTarget != null)
				sb.Append($"E{enPassantTarget.X}{enPassantTarget.Y}");
			else
				sb.Append("E--");

			// Add move count to differentiate positions reached by different paths
			sb.Append($"M{moveRecords.Count}");

			// Add board state (compact representation)
			for (int y = 0; y < 8; y++)
			{
				for (int x = 0; x < 8; x++)
				{
					Piece p = pieces[x, y];
					if (p == null)
						sb.Append('-');
					else
					{
						// Include piece type and color
						sb.Append(p.Symbol);
					}
				}
			}

			// Include king positions explicitly for verification
			if (whiteKingPosition != null)
				sb.Append($"WK{whiteKingPosition.X}{whiteKingPosition.Y}");
			if (blackKingPosition != null)
				sb.Append($"BK{blackKingPosition.X}{blackKingPosition.Y}");

			return sb.ToString();
		}

		// Calculate score based on control of central squares
		private int CalculateCenterControl(PieceColor color)
		{
			int score = 0;
			PieceColor opponent = color == PieceColor.White ? PieceColor.Black : PieceColor.White;

			// Define center squares
			Position[] centerSquares = new Position[]
			{
		new Position(3, 3), new Position(4, 3),
		new Position(3, 4), new Position(4, 4)
			};

			// Count how many pieces attack center squares
			foreach (var square in centerSquares)
			{
				if (IsSquareAttackedBy(square.X, square.Y, color))
					score += 10;
				if (IsSquareAttackedBy(square.X, square.Y, opponent))
					score -= 10;
			}

			return score;
		}

		// Check if a square is attacked by a piece of the given color
		private bool IsSquareAttackedBy(int x, int y, PieceColor attackerColor)
		{
			var attackers = GetPiecesByColor(attackerColor);

			foreach (var entry in attackers)
			{
				Piece piece = entry.Key;
				Position pos = entry.Value;

				Move move = new Move(pos.X, pos.Y, x, y, new DummyPlayer(attackerColor));

				if (piece.IsValidMove(move, this))
					return true;
			}

			return false;
		}

		// Calculate development score (knights and bishops out, castled, pawns advanced)
		private int CalculateDevelopment(PieceColor color)
		{
			int score = 0;
			int homeRank = color == PieceColor.White ? 0 : 7;

			var pieces = GetPiecesByColor(color);

			// Knights and bishops developed
			foreach (var entry in pieces)
			{
				Piece piece = entry.Key;
				Position pos = entry.Value;

				if (piece is Knight || piece is Bishop)
				{
					if (pos.Y != homeRank)
						score += 10; // Piece is off the back rank
				}
			}

			// Castling completed or available
			if (color == PieceColor.White)
			{
				if (whiteKingMoved && whiteKingPosition != null &&
					(whiteKingPosition.X == 2 || whiteKingPosition.X == 6))
					score += 40; // Already castled
				else if (!whiteKingMoved && (!whiteRookAMoved || !whiteRookHMoved))
					score += 20; // Can still castle
			}
			else
			{
				if (blackKingMoved && blackKingPosition != null &&
					(blackKingPosition.X == 2 || blackKingPosition.X == 6))
					score += 40; // Already castled
				else if (!blackKingMoved && (!blackRookAMoved || !blackRookHMoved))
					score += 20; // Can still castle
			}
			// Central pawns advanced
			for (int x = 3; x <= 4; x++)
			{
				for (int y = 2; y <= 5; y++)
				{
					Piece p = this.pieces[x, y]; // Use `this.` to avoid confusion
					if (p is Pawn && p.Color == color)
						score += 10;
				}
			}


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