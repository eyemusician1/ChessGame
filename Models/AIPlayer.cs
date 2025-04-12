// Models/AIPlayer.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ChessGame.Models
{
	/// <summary>
	/// Event arguments for AI thinking progress
	/// </summary>
	public class AIThinkingEventArgs : EventArgs
	{
		public int ProgressPercentage { get; }

		public AIThinkingEventArgs(int progressPercentage)
		{
			ProgressPercentage = progressPercentage;
		}
	}

	/// <summary>
	/// AI player implementation
	/// </summary>
	public class AIPlayer : Player
	{
		private readonly Random random = new Random();
		private readonly int depth;
		private int movesEvaluated;
		private int totalMovesToEvaluate;

		// Transposition table for caching evaluations
		private Dictionary<string, int> transpositionTable = new Dictionary<string, int>();

		public event EventHandler<AIThinkingEventArgs> ThinkingProgress;

		public int Depth => depth;

		public AIPlayer(PieceColor color, int depth = 3) : base(color)
		{
			this.depth = Math.Max(1, Math.Min(5, depth)); // Ensure depth is between 1 and 5
		}

		public override Move GetMove(Board board)
		{
			// Clear the transposition table for a new search
			transpositionTable.Clear();

			// Use minimax algorithm to find the best move
			Move bestMove = null;
			int bestScore = int.MinValue;

			// Get all possible moves for the AI
			List<Move> possibleMoves = GetAllPossibleMoves(board, Color);

			// Pre-sort moves to improve alpha-beta pruning
			possibleMoves = PreSortMoves(board, possibleMoves);

			// Initialize progress tracking
			movesEvaluated = 0;
			totalMovesToEvaluate = possibleMoves.Count;

			// Evaluate each move
			foreach (Move move in possibleMoves)
			{
				// Make a temporary move
				board.MakeMove(move);

				// Evaluate the move using minimax
				int score = Minimax(board, depth - 1, false, int.MinValue, int.MaxValue, Color);

				// Undo the move
				board.UndoMove();

				// Update best move
				if (score > bestScore)
				{
					bestScore = score;
					bestMove = move;
				}

				// Update progress
				movesEvaluated++;
				OnThinkingProgress(new AIThinkingEventArgs(movesEvaluated * 100 / totalMovesToEvaluate));
			}

			// Final progress update
			OnThinkingProgress(new AIThinkingEventArgs(100));

			return bestMove;
		}

		protected virtual void OnThinkingProgress(AIThinkingEventArgs e)
		{
			ThinkingProgress?.Invoke(this, e);
		}

		private List<Move> GetAllPossibleMoves(Board board, PieceColor color)
		{
			List<Move> possibleMoves = new List<Move>();

			// Create a copy of the piece positions to avoid modification during enumeration
			var piecePositionsCopy = board.GetPiecesByColor(color).ToList();

			foreach (var entry in piecePositionsCopy)
			{
				Piece piece = entry.Key;
				Position pos = entry.Value;

				// Skip if piece is null or position is invalid
				if (piece == null || pos == null || pos.X < 0 || pos.X > 7 || pos.Y < 0 || pos.Y > 7)
					continue;

				for (int toX = 0; toX < 8; toX++)
				{
					for (int toY = 0; toY < 8; toY++)
					{
						Move move = new Move(pos.X, pos.Y, toX, toY, new DummyPlayer(color));

						if (board.IsValidMove(move))
						{
							possibleMoves.Add(move);
						}
					}
				}
			}

			return possibleMoves;
		}

		// Pre-sort moves to improve alpha-beta pruning efficiency
		private List<Move> PreSortMoves(Board board, List<Move> moves)
		{
			return moves.OrderByDescending(move =>
			{
				int score = 0;

				// Prioritize captures
				Piece capturedPiece = board.GetPiece(move.ToX, move.ToY);
				if (capturedPiece != null)
				{
					score += GetPieceValue(capturedPiece);
				}

				// Prioritize center control for pawns
				Piece piece = board.GetPiece(move.FromX, move.FromY);
				if (piece is Pawn)
				{
					int centerDistance = Math.Abs(move.ToX - 3) + Math.Abs(move.ToX - 4) +
										Math.Abs(move.ToY - 3) + Math.Abs(move.ToY - 4);
					score += (8 - centerDistance) * 5;
				}

				// Add some randomness to avoid predictable play
				score += random.Next(5);

				return score;
			}).ToList();
		}

		private int Minimax(Board board, int depth, bool maximizingPlayer, int alpha, int beta, PieceColor maximizingColor)
		{
			// Generate a hash key for the current board position
			string boardKey = GenerateBoardKey(board, depth, maximizingPlayer);

			// Check if we've already evaluated this position
			if (transpositionTable.ContainsKey(boardKey))
			{
				return transpositionTable[boardKey];
			}

			// Terminal conditions
			if (depth == 0)
				return QuiescenceSearch(board, 3, alpha, beta, maximizingColor); // Use quiescence search for better evaluation

			if (board.IsCheckmate(maximizingColor))
				return -10000; // Being checkmated is very bad

			if (board.IsCheckmate(maximizingColor == PieceColor.White ? PieceColor.Black : PieceColor.White))
				return 10000; // Checkmating opponent is very good

			if (board.IsStalemate())
				return 0; // Stalemate is neutral

			PieceColor currentColor = maximizingPlayer ? maximizingColor :
									 (maximizingColor == PieceColor.White ? PieceColor.Black : PieceColor.White);

			List<Move> possibleMoves = GetAllPossibleMoves(board, currentColor);

			// Early exit if no moves
			if (possibleMoves.Count == 0)
				return maximizingPlayer ? -9000 : 9000;

			// Order moves to improve alpha-beta pruning
			possibleMoves = OrderMoves(board, possibleMoves);

			if (maximizingPlayer)
			{
				int maxEval = int.MinValue;

				foreach (Move move in possibleMoves)
				{
					board.MakeMove(move);
					int eval = Minimax(board, depth - 1, false, alpha, beta, maximizingColor);
					board.UndoMove();

					maxEval = Math.Max(maxEval, eval);
					alpha = Math.Max(alpha, eval);

					if (beta <= alpha)
						break; // Beta cutoff
				}

				// Store the result in the transposition table
				transpositionTable[boardKey] = maxEval;

				return maxEval;
			}
			else
			{
				int minEval = int.MaxValue;

				foreach (Move move in possibleMoves)
				{
					board.MakeMove(move);
					int eval = Minimax(board, depth - 1, true, alpha, beta, maximizingColor);
					board.UndoMove();

					minEval = Math.Min(minEval, eval);
					beta = Math.Min(beta, eval);

					if (beta <= alpha)
						break; // Alpha cutoff
				}

				// Store the result in the transposition table
				transpositionTable[boardKey] = minEval;

				return minEval;
			}
		}

		// Quiescence search to avoid horizon effect
		private int QuiescenceSearch(Board board, int depth, int alpha, int beta, PieceColor maximizingColor)
		{
			// Base evaluation
			int standPat = board.Evaluate(maximizingColor);

			// Stand-pat cutoff
			if (standPat >= beta)
				return beta;
			if (alpha < standPat)
				alpha = standPat;

			// Stop if maximum depth reached
			if (depth <= 0)
				return standPat;

			// Get all capture moves
			List<Move> captureMoves = GetCaptureMoves(board, maximizingColor);

			// Order moves
			captureMoves = OrderMoves(board, captureMoves);

			foreach (Move move in captureMoves)
			{
				board.MakeMove(move);
				int score = -QuiescenceSearch(board, depth - 1, -beta, -alpha,
					maximizingColor == PieceColor.White ? PieceColor.Black : PieceColor.White);
				board.UndoMove();

				if (score >= beta)
					return beta;
				if (score > alpha)
					alpha = score;
			}

			return alpha;
		}

		// Get only capture moves for quiescence search
		private List<Move> GetCaptureMoves(Board board, PieceColor color)
		{
			List<Move> captureMoves = new List<Move>();

			// Create a copy of the piece positions to avoid modification during enumeration
			var piecePositionsCopy = board.GetPiecesByColor(color).ToList();

			foreach (var entry in piecePositionsCopy)
			{
				Piece piece = entry.Key;
				Position pos = entry.Value;

				// Skip if piece is null or position is invalid
				if (piece == null || pos == null || pos.X < 0 || pos.X > 7 || pos.Y < 0 || pos.Y > 7)
					continue;

				for (int toX = 0; toX < 8; toX++)
				{
					for (int toY = 0; toY < 8; toY++)
					{
						// Only consider moves that capture a piece
						Piece targetPiece = board.GetPiece(toX, toY);
						if (targetPiece != null && targetPiece.Color != color)
						{
							Move move = new Move(pos.X, pos.Y, toX, toY, new DummyPlayer(color));

							if (board.IsValidMove(move))
							{
								captureMoves.Add(move);
							}
						}
					}
				}
			}

			return captureMoves;
		}

		// Generate a unique key for the current board position
		private string GenerateBoardKey(Board board, int depth, bool maximizingPlayer)
		{
			// Simple implementation - can be improved with Zobrist hashing for better performance
			string key = "";

			for (int y = 0; y < 8; y++)
			{
				for (int x = 0; x < 8; x++)
				{
					Piece piece = board.GetPiece(x, y);
					if (piece == null)
						key += "-";
					else
						key += piece.Symbol;
				}
			}

			key += "_" + depth + "_" + (maximizingPlayer ? "1" : "0");

			return key;
		}

		private List<Move> OrderMoves(Board board, List<Move> moves)
		{
			// Score moves for better alpha-beta pruning
			// Captures and checks are tried first
			return moves.OrderByDescending(move =>
			{
				int score = 0;

				// Prioritize captures
				Piece capturedPiece = board.GetPiece(move.ToX, move.ToY);
				if (capturedPiece != null)
				{
					score += GetPieceValue(capturedPiece) - GetPieceValue(board.GetPiece(move.FromX, move.FromY)) / 10;
				}

				// Check if move gives check
				board.MakeMove(move);

				// Get the moved piece safely
				Piece movedPiece = board.GetPiece(move.ToX, move.ToY);

				// Only check for check if we have a valid piece
				if (movedPiece != null)
				{
					PieceColor opponentColor = movedPiece.Color == PieceColor.White ?
										   PieceColor.Black : PieceColor.White;

					if (board.IsInCheck(opponentColor))
					{
						score += 50;
					}
				}

				board.UndoMove();

				return score;
			}).ToList();
		}

		private int GetPieceValue(Piece piece)
		{
			if (piece == null) return 0;
			if (piece is Pawn) return 100;
			if (piece is Knight) return 320;
			if (piece is Bishop) return 330;
			if (piece is Rook) return 500;
			if (piece is Queen) return 900;
			if (piece is King) return 20000;

			return 0;
		}
	}
}

