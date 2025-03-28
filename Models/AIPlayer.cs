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

		public event EventHandler<AIThinkingEventArgs> ThinkingProgress;

		public int Depth => depth;

		public AIPlayer(PieceColor color, int depth = 3) : base(color)
		{
			this.depth = Math.Max(1, Math.Min(5, depth)); // Ensure depth is between 1 and 5
		}

		public override Move GetMove(Board board)
		{
			// Use minimax algorithm to find the best move
			Move bestMove = null;
			int bestScore = int.MinValue;

			// Get all possible moves for the AI
			List<Move> possibleMoves = GetAllPossibleMoves(board, Color);

			// Randomize move order for more varied play
			possibleMoves = possibleMoves.OrderBy(x => random.Next()).ToList();

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
				board.UndoMove(move);

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

			for (int fromX = 0; fromX < 8; fromX++)
			{
				for (int fromY = 0; fromY < 8; fromY++)
				{
					Piece piece = board.GetPiece(fromX, fromY);

					if (piece != null && piece.Color == color)
					{
						for (int toX = 0; toX < 8; toX++)
						{
							for (int toY = 0; toY < 8; toY++)
							{
								Move move = new Move(fromX, fromY, toX, toY, new DummyPlayer(color));

								if (board.IsValidMove(move))
								{
									possibleMoves.Add(move);
								}
							}
						}
					}
				}
			}

			return possibleMoves;
		}

		private int Minimax(Board board, int depth, bool maximizingPlayer, int alpha, int beta, PieceColor maximizingColor)
		{
			// Terminal conditions
			if (depth == 0)
				return board.Evaluate(maximizingColor);

			if (board.IsCheckmate(maximizingColor))
				return -10000; // Being checkmated is very bad

			if (board.IsCheckmate(maximizingColor == PieceColor.White ? PieceColor.Black : PieceColor.White))
				return 10000; // Checkmating opponent is very good

			if (board.IsStalemate())
				return 0; // Stalemate is neutral

			PieceColor currentColor = maximizingPlayer ? maximizingColor :
									 (maximizingColor == PieceColor.White ? PieceColor.Black : PieceColor.White);

			List<Move> possibleMoves = GetAllPossibleMoves(board, currentColor);

			// Order moves to improve alpha-beta pruning
			if (maximizingPlayer)
			{
				// Captures and checks first for maximizing player
				possibleMoves = OrderMoves(board, possibleMoves);
			}
			else
			{
				// Captures and checks first for minimizing player
				possibleMoves = OrderMoves(board, possibleMoves);
			}

			if (maximizingPlayer)
			{
				int maxEval = int.MinValue;

				foreach (Move move in possibleMoves)
				{
					board.MakeMove(move);
					int eval = Minimax(board, depth - 1, false, alpha, beta, maximizingColor);
					board.UndoMove(move);

					maxEval = Math.Max(maxEval, eval);
					alpha = Math.Max(alpha, eval);

					if (beta <= alpha)
						break; // Beta cutoff
				}

				return maxEval;
			}
			else
			{
				int minEval = int.MaxValue;

				foreach (Move move in possibleMoves)
				{
					board.MakeMove(move);
					int eval = Minimax(board, depth - 1, true, alpha, beta, maximizingColor);
					board.UndoMove(move);

					minEval = Math.Min(minEval, eval);
					beta = Math.Min(beta, eval);

					if (beta <= alpha)
						break; // Alpha cutoff
				}

				return minEval;
			}
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
				if (board.IsInCheck(board.GetPiece(move.ToX, move.ToY).Color == PieceColor.White ?
								   PieceColor.Black : PieceColor.White))
				{
					score += 50;
				}
				board.UndoMove(move);

				return score;
			}).ToList();
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
	}
}

