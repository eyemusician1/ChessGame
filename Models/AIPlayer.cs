// Models/AIPlayer.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChessGame.Models
{
	/// <summary>
	/// Event arguments for AI thinking progress updates
	/// This allows the UI to display a progress bar or other visual indicator
	/// to show the user that the AI is "thinking"
	/// </summary>
	public class AIThinkingEventArgs : EventArgs
	{
		/// <summary>
		/// The percentage of the AI's thinking process that has been completed (0-100)
		/// </summary>
		public int ProgressPercentage { get; }

		public AIThinkingEventArgs(int progressPercentage)
		{
			ProgressPercentage = progressPercentage;
		}
	}

	/// <summary>
	/// AI player implementation using the Minimax algorithm with Alpha-Beta pruning
	/// 
	/// The Minimax algorithm is a decision-making algorithm used in two-player games to find
	/// the optimal move. It works by recursively evaluating all possible moves and their outcomes,
	/// assuming that both players play optimally.
	/// 
	/// Alpha-Beta pruning is an optimization technique that reduces the number of nodes
	/// evaluated in the search tree by eliminating branches that cannot affect the final decision.
	/// </summary>
	public class AIPlayer : Player
	{
		private readonly Random random = new Random();

		/// <summary>
		/// The maximum depth of the Minimax search tree
		/// Higher depth means the AI looks more moves ahead but takes longer to compute
		/// </summary>
		private readonly int depth;

		/// <summary>
		/// Tracking variables for progress reporting to UI
		/// </summary>
		private int movesEvaluated;
		private int totalMovesToEvaluate;

		/// <summary>
		/// Transposition table for caching board evaluations
		/// This dramatically improves performance by preventing re-evaluation of positions
		/// that have already been calculated
		/// 
		/// Key: A string representation of the board position + search depth + player turn
		/// Value: The evaluated score for that position
		/// </summary>
		private Dictionary<string, int> transpositionTable = new Dictionary<string, int>();

		/// <summary>
		/// Event fired to report AI thinking progress to UI components
		/// UI can subscribe to this event to update progress bars or other indicators
		/// </summary>
		public event EventHandler<AIThinkingEventArgs> ThinkingProgress;

		/// <summary>
		/// The search depth of this AI player
		/// </summary>
		public int Depth => depth;

		/// <summary>
		/// Creates a new AI player
		/// </summary>
		/// <param name="color">The color of pieces this AI will play</param>
		/// <param name="depth">How many moves ahead the AI will look (1-5)</param>
		public AIPlayer(PieceColor color, int depth = 3) : base(color)
		{
			// Ensure depth is reasonable (1-5)
			// Depths > 5 become very slow without additional optimizations
			this.depth = Math.Max(1, Math.Min(5, depth));
		}

		/// <summary>
		/// Determines the best move for the AI player given the current board state
		/// This is the main entry point called by the UI/game controller when it's the AI's turn
		/// </summary>
		/// <param name="board">The current chess board</param>
		/// <returns>The best move according to the AI's evaluation</returns>
		public override Move GetMove(Board board)
		{
			// Start with a fresh transposition table for this move
			// This prevents the table from growing too large over a long game
			transpositionTable.Clear();

			Move bestMove = null;
			int bestScore = int.MinValue;

			// Get all possible legal moves for the AI's color
			List<Move> possibleMoves = GetAllPossibleMoves(board, Color);

			// Pre-sort moves to improve alpha-beta pruning efficiency
			// This puts likely good moves first, which helps pruning eliminate more branches
			possibleMoves = PreSortMoves(board, possibleMoves);

			// Initialize progress tracking for UI updates
			movesEvaluated = 0;
			totalMovesToEvaluate = possibleMoves.Count;

			// If no moves are possible, return null (checkmate or stalemate)
			if (possibleMoves.Count == 0)
				return null;

			// Try each move and evaluate the resulting position
			foreach (Move move in possibleMoves)
			{
				// Make a temporary move
				board.MakeMove(move);

				// Evaluate the move using minimax algorithm
				// The 'false' parameter means we're switching to the opponent's perspective
				// (minimizing player) for the next level of the tree
				int score = Minimax(board, depth - 1, false, int.MinValue, int.MaxValue, Color);

				// Undo the move to restore the board
				board.UndoMove();

				// Update best move if this one is better
				if (score > bestScore)
				{
					bestScore = score;
					bestMove = move;
				}
				// For equal scores, randomly select to add variety
				else if (score == bestScore && random.Next(3) == 0)
				{
					bestMove = move;
				}

				// Update progress for UI
				movesEvaluated++;
				OnThinkingProgress(new AIThinkingEventArgs(movesEvaluated * 100 / totalMovesToEvaluate));
			}

			// Final progress update
			OnThinkingProgress(new AIThinkingEventArgs(100));

			return bestMove;
		}

		/// <summary>
		/// Raises the ThinkingProgress event with the current progress percentage
		/// UI components can subscribe to this event to update visual indicators
		/// </summary>
		/// <param name="e">Event arguments containing progress percentage</param>
		protected virtual void OnThinkingProgress(AIThinkingEventArgs e)
		{
			ThinkingProgress?.Invoke(this, e);
		}

		/// <summary>
		/// Finds all possible legal moves for a specific color
		/// </summary>
		/// <param name="board">The current board state</param>
		/// <param name="color">The color to get moves for</param>
		/// <returns>A list of all legal moves for the specified color</returns>
		private List<Move> GetAllPossibleMoves(Board board, PieceColor color)
		{
			List<Move> possibleMoves = new List<Move>();

			// Get all pieces of the specified color
			// Create a copy of the piece positions to avoid modification during enumeration
			var piecePositionsCopy = board.GetPiecesByColor(color).ToList();

			// For each piece, find all its possible moves
			foreach (var entry in piecePositionsCopy)
			{
				Piece piece = entry.Key;
				Position pos = entry.Value;

				// Skip if piece is null or position is invalid
				if (piece == null || pos == null || pos.X < 0 || pos.X > 7 || pos.Y < 0 || pos.Y > 7)
					continue;

				// Try all possible destination squares
				for (int toX = 0; toX < 8; toX++)
				{
					for (int toY = 0; toY < 8; toY++)
					{
						// Create a potential move
						Move move = new Move(pos.X, pos.Y, toX, toY, new DummyPlayer(color));

						// Check if the move is legal according to chess rules
						if (board.IsValidMove(move))
						{
							possibleMoves.Add(move);
						}
					}
				}
			}

			return possibleMoves;
		}

		/// <summary>
		/// Pre-sorts moves to improve alpha-beta pruning efficiency
		/// Good moves are tried first, which increases the chances of finding good cutoffs
		/// </summary>
		/// <param name="board">The current board state</param>
		/// <param name="moves">List of possible moves</param>
		/// <returns>Sorted list of moves (best first)</returns>
		private List<Move> PreSortMoves(Board board, List<Move> moves)
		{
			return moves.OrderByDescending(move =>
			{
				int score = 0;

				// Prioritize captures
				Piece capturedPiece = board.GetPiece(move.ToX, move.ToY);
				if (capturedPiece != null)
				{
					// Capture value is based on the value of the captured piece
					// MVV-LVA (Most Valuable Victim - Least Valuable Aggressor)
					score += GetPieceValue(capturedPiece) - (GetPieceValue(board.GetPiece(move.FromX, move.FromY)) / 10);
				}

				// Prioritize center control for pawns
				Piece piece = board.GetPiece(move.FromX, move.FromY);
				if (piece is Pawn)
				{
					// Calculate Manhattan distance from center (lower is better)
					int centerDistance = Math.Abs(move.ToX - 3) + Math.Abs(move.ToX - 4) +
										Math.Abs(move.ToY - 3) + Math.Abs(move.ToY - 4);
					score += (8 - centerDistance) * 5;
				}

				// Knights are good on central squares
				if (piece is Knight)
				{
					// Bonus for knights near the center
					if ((move.ToX >= 2 && move.ToX <= 5) && (move.ToY >= 2 && move.ToY <= 5))
					{
						score += 20;
					}
				}

				// Add some randomness to avoid predictable play
				score += random.Next(5);

				return score;
			}).ToList();
		}

		/// <summary>
		/// The Minimax algorithm with alpha-beta pruning
		/// This recursively evaluates all possible moves to find the best one
		/// 
		/// Alpha-Beta pruning works by maintaining two values:
		/// - Alpha: the minimum score that the maximizing player is guaranteed
		/// - Beta: the maximum score that the minimizing player is guaranteed
		/// 
		/// If a move is found that would cause beta <= alpha, that branch is pruned
		/// because the opponent would never allow that position to be reached
		/// </summary>
		/// <param name="board">Current board state</param>
		/// <param name="depth">Remaining depth to search</param>
		/// <param name="maximizingPlayer">True if current player is maximizing, false if minimizing</param>
		/// <param name="alpha">Alpha value for pruning</param>
		/// <param name="beta">Beta value for pruning</param>
		/// <param name="maximizingColor">The color of the maximizing player</param>
		/// <returns>The best score for the current player</returns>
		private int Minimax(Board board, int depth, bool maximizingPlayer, int alpha, int beta, PieceColor maximizingColor)
		{
			// Generate a hash key for the current board position
			string boardKey = GenerateBoardKey(board, depth, maximizingPlayer);

			// Check transposition table first - if we've already evaluated this position, reuse the result
			if (transpositionTable.ContainsKey(boardKey))
			{
				return transpositionTable[boardKey];
			}

			// Terminal conditions - stop recursion at leaf nodes

			// If maximum depth reached, use quiescence search for more accurate evaluation
			if (depth == 0)
				return QuiescenceSearch(board, 3, alpha, beta, maximizingColor);

			// Game ending conditions
			if (board.IsCheckmate(maximizingColor))
				return -10000; // Being checkmated is very bad

			if (board.IsCheckmate(maximizingColor == PieceColor.White ? PieceColor.Black : PieceColor.White))
				return 10000; // Checkmating opponent is very good

			if (board.IsStalemate())
				return 0; // Stalemate is neutral (draw)

			// Determine whose turn it is
			PieceColor currentColor = maximizingPlayer ? maximizingColor :
									 (maximizingColor == PieceColor.White ? PieceColor.Black : PieceColor.White);

			// Get all possible moves for the current player
			List<Move> possibleMoves = GetAllPossibleMoves(board, currentColor);

			// If no moves are available, it's a terminal position
			if (possibleMoves.Count == 0)
				return maximizingPlayer ? -9000 : 9000;

			// Order moves to improve alpha-beta pruning
			possibleMoves = OrderMoves(board, possibleMoves);

			// Maximizing player's turn (AI is trying to maximize its score)
			if (maximizingPlayer)
			{
				int maxEval = int.MinValue;

				foreach (Move move in possibleMoves)
				{
					// Make move, evaluate, and undo
					board.MakeMove(move);
					int eval = Minimax(board, depth - 1, false, alpha, beta, maximizingColor);
					board.UndoMove();

					// Update best evaluation
					maxEval = Math.Max(maxEval, eval);

					// Update alpha value
					alpha = Math.Max(alpha, eval);

					// Beta cutoff - if this branch is worse than what the opponent can force,
					// we can stop evaluating this branch
					if (beta <= alpha)
						break;
				}

				// Store result in transposition table
				transpositionTable[boardKey] = maxEval;

				return maxEval;
			}
			// Minimizing player's turn (opponent is trying to minimize AI's score)
			else
			{
				int minEval = int.MaxValue;

				foreach (Move move in possibleMoves)
				{
					// Make move, evaluate, and undo
					board.MakeMove(move);
					int eval = Minimax(board, depth - 1, true, alpha, beta, maximizingColor);
					board.UndoMove();

					// Update best evaluation
					minEval = Math.Min(minEval, eval);

					// Update beta value
					beta = Math.Min(beta, eval);

					// Alpha cutoff - if this branch is better than what the AI can force,
					// we can stop evaluating this branch
					if (beta <= alpha)
						break;
				}

				// Store result in transposition table
				transpositionTable[boardKey] = minEval;

				return minEval;
			}
		}

		/// <summary>
		/// Quiescence search to avoid horizon effect
		/// 
		/// The "horizon effect" occurs when the AI evaluates a position right 
		/// before an important capture/exchange would happen, leading to inaccurate evaluations.
		/// Quiescence search continues evaluating captures beyond the regular search depth
		/// to ensure the position is "quiet" (stable) before evaluating.
		/// </summary>
		/// <param name="board">Current board state</param>
		/// <param name="depth">Remaining quiescence depth</param>
		/// <param name="alpha">Alpha value for pruning</param>
		/// <param name="beta">Beta value for pruning</param>
		/// <param name="maximizingColor">The color of the maximizing player</param>
		/// <returns>The best score for the current player</returns>
		private int QuiescenceSearch(Board board, int depth, int alpha, int beta, PieceColor maximizingColor)
		{
			// Get static evaluation of the current position
			int standPat = board.Evaluate(maximizingColor);

			// Stand-pat cutoff - if the current position is already so good/bad
			// that we don't need to look at captures
			if (standPat >= beta)
				return beta;
			if (alpha < standPat)
				alpha = standPat;

			// If no more depth, return the evaluation
			if (depth <= 0)
				return standPat;

			// Only consider capture moves in quiescence search
			List<Move> captureMoves = GetCaptureMoves(board, maximizingColor);

			// Order captures by value for more efficient pruning
			captureMoves = OrderMoves(board, captureMoves);

			// Try each capture move
			foreach (Move move in captureMoves)
			{
				board.MakeMove(move);

				// Recursive call with colors swapped
				int score = -QuiescenceSearch(board, depth - 1, -beta, -alpha,
					maximizingColor == PieceColor.White ? PieceColor.Black : PieceColor.White);

				board.UndoMove();

				// Update alpha and check for beta cutoff
				if (score >= beta)
					return beta;
				if (score > alpha)
					alpha = score;
			}

			return alpha;
		}

		/// <summary>
		/// Gets only capture moves for the quiescence search
		/// </summary>
		/// <param name="board">Current board state</param>
		/// <param name="color">The color to get moves for</param>
		/// <returns>List of all legal capture moves</returns>
		private List<Move> GetCaptureMoves(Board board, PieceColor color)
		{
			List<Move> captureMoves = new List<Move>();

			// Get all pieces of the current color
			var piecePositionsCopy = board.GetPiecesByColor(color).ToList();

			foreach (var entry in piecePositionsCopy)
			{
				Piece piece = entry.Key;
				Position pos = entry.Value;

				// Skip if piece is null or position is invalid
				if (piece == null || pos == null || pos.X < 0 || pos.X > 7 || pos.Y < 0 || pos.Y > 7)
					continue;

				// Try all possible destination squares
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

		/// <summary>
		/// Generates a unique key for the current board position
		/// This is used for the transposition table
		/// </summary>
		/// <param name="board">Current board state</param>
		/// <param name="depth">Current search depth</param>
		/// <param name="maximizingPlayer">Whether it's the maximizing player's turn</param>
		/// <returns>A string key representing the board position</returns>
		private string GenerateBoardKey(Board board, int depth, bool maximizingPlayer)
		{
			// A more efficient implementation would use Zobrist hashing
			// This simple implementation concatenates piece positions
			StringBuilder key = new StringBuilder();

			for (int y = 0; y < 8; y++)
			{
				for (int x = 0; x < 8; x++)
				{
					Piece piece = board.GetPiece(x, y);
					if (piece == null)
						key.Append("-");
					else
						key.Append(piece.Symbol);
				}
			}

			// Include depth and player in the key since evaluations differ by depth and turn
			key.Append("_").Append(depth).Append("_").Append(maximizingPlayer ? "1" : "0");

			return key.ToString();
		}

		/// <summary>
		/// Orders moves based on their likely quality to improve alpha-beta pruning
		/// </summary>
		/// <param name="board">Current board state</param>
		/// <param name="moves">List of possible moves</param>
		/// <returns>Sorted list of moves (best first)</returns>
		private List<Move> OrderMoves(Board board, List<Move> moves)
		{
			// Score moves for better alpha-beta pruning
			// Captures and checks are tried first
			return moves.OrderByDescending(move =>
			{
				int score = 0;

				// Prioritize captures based on MVV-LVA (Most Valuable Victim - Least Valuable Aggressor)
				Piece capturedPiece = board.GetPiece(move.ToX, move.ToY);
				if (capturedPiece != null)
				{
					// More valuable for capturing valuable pieces with less valuable pieces
					score += GetPieceValue(capturedPiece) - GetPieceValue(board.GetPiece(move.FromX, move.FromY)) / 10;
				}

				// Check if move gives check (bonus for this)
				board.MakeMove(move);

				// Get the moved piece
				Piece movedPiece = board.GetPiece(move.ToX, move.ToY);

				// Only check for check if we have a valid piece
				if (movedPiece != null)
				{
					PieceColor opponentColor = movedPiece.Color == PieceColor.White ?
										   PieceColor.Black : PieceColor.White;

					// Bonus points for putting opponent in check
					if (board.IsInCheck(opponentColor))
					{
						score += 50;
					}
				}

				board.UndoMove();

				// Bonus for advancing pawns toward promotion
				Piece piece = board.GetPiece(move.FromX, move.FromY);
				if (piece is Pawn)
				{
					// White pawns want to move toward row 0, black toward row 7
					int promotionRow = piece.Color == PieceColor.White ? 0 : 7;
					int distanceToPromotion = Math.Abs(move.ToY - promotionRow);
					score += (7 - distanceToPromotion) * 10;
				}

				return score;
			}).ToList();
		}

		/// <summary>
		/// Returns the material value of a chess piece
		/// </summary>
		/// <param name="piece">The piece to evaluate</param>
		/// <returns>The value of the piece</returns>
		private int GetPieceValue(Piece piece)
		{
			if (piece == null) return 0;

			// Standard piece values (centipawns)
			if (piece is Pawn) return 100;
			if (piece is Knight) return 320;
			if (piece is Bishop) return 330;
			if (piece is Rook) return 500;
			if (piece is Queen) return 900;
			if (piece is King) return 20000; // High value to ensure king safety

			return 0;
		}

		/// <summary>
		/// Asynchronous version of GetMove that can be called from UI without freezing
		/// </summary>
		/// <param name="board">Current board state</param>
		/// <param name="cancellationToken">Optional token to cancel operation</param>
		/// <returns>Task containing the best move</returns>
		public async Task<Move> GetMoveAsync(Board board, CancellationToken cancellationToken = default)
		{
			// Run the AI calculation on a background thread
			return await Task.Run(() => GetMove(board), cancellationToken);
		}
	}
}