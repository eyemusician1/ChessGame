// Models/AIPlayer.cs
// This file implements an artificial intelligence player for a chess game
// using the Minimax algorithm with Alpha-Beta pruning optimization
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChessGame.Models
{
	/// <summary>
	/// Event arguments for AI thinking progress updates.
	/// This allows the UI to display a progress bar or other visual indicator
	/// to show the user that the AI is "thinking" - enhancing user experience by
	/// providing feedback during potentially long calculation periods.
	/// </summary>
	public class AIThinkingEventArgs : EventArgs
	{
		/// <summary>
		/// The percentage of the AI's thinking process that has been completed (0-100)
		/// This value can be directly mapped to a progress bar width in the UI
		/// </summary>
		public int ProgressPercentage { get; }

		/// <summary>
		/// Initializes a new instance of the AIThinkingEventArgs class
		/// </summary>
		/// <param name="progressPercentage">Percentage of thinking completed (0-100)</param>
		public AIThinkingEventArgs(int progressPercentage)
		{
			ProgressPercentage = progressPercentage;
		}
	}

	/// <summary>
	/// AI player implementation using the Minimax algorithm with Alpha-Beta pruning.
	/// 
	/// The Minimax algorithm is a recursive decision-making algorithm used in two-player 
	/// zero-sum games (like chess) to find the optimal move. It works by evaluating all possible 
	/// moves and their outcomes, assuming that both players play optimally.
	/// 
	/// Alpha-Beta pruning is an optimization technique that dramatically reduces the 
	/// number of nodes evaluated in the search tree by eliminating branches that cannot 
	/// affect the final decision, thereby allowing deeper searches in the same time.
	/// 
	/// This AI also implements several advanced optimizations including:
	/// 1. Move ordering - Evaluating promising moves first improves pruning efficiency
	/// 2. Transposition table - Caching already evaluated positions prevents redundant calculations
	/// 3. Quiescence search - Avoiding the horizon effect by extending search in volatile positions
	/// 4. MVV-LVA (Most Valuable Victim - Least Valuable Aggressor) - Prioritizing good captures
	/// </summary>
	public class AIPlayer : Player
	{
		// Random number generator used to add variety to AI play
		// This prevents predictable move selection when multiple moves have the same evaluation
		private readonly Random random = new Random();

		/// <summary>
		/// The maximum depth of the Minimax search tree.
		/// Higher depth means the AI looks more moves ahead but takes longer to compute.
		/// Chess has approximately 35 legal moves per position on average, so the search space
		/// grows exponentially with depth (roughly 35^depth positions to evaluate).
		/// </summary>
		private readonly int depth;

		/// <summary>
		/// Tracks how many moves have been evaluated so far in the current search.
		/// This is used to calculate progress percentage for UI updates.
		/// </summary>
		private int movesEvaluated;

		/// <summary>
		/// The total number of top-level moves that need evaluation.
		/// Used as the denominator when calculating progress percentage.
		/// </summary>
		private int totalMovesToEvaluate;

		/// <summary>
		/// Transposition table for caching board evaluations.
		/// This dramatically improves performance by preventing re-evaluation of positions
		/// that have already been calculated.
		/// 
		/// This is one of the most important optimizations in the AI, as chess positions
		/// are frequently reached via different move orders (transpositions).
		/// 
		/// Key: A string representation of the board position + search depth + player turn
		/// Value: The evaluated score for that position
		/// </summary>
		private Dictionary<string, int> transpositionTable = new Dictionary<string, int>();

		/// <summary>
		/// Event fired to report AI thinking progress to UI components.
		/// UI can subscribe to this event to update progress bars or other indicators,
		/// giving the user visual feedback during AI calculations.
		/// </summary>
		public event EventHandler<AIThinkingEventArgs> ThinkingProgress;

		/// <summary>
		/// The search depth of this AI player.
		/// Exposes the depth parameter to allow the game controller or UI to
		/// display the current AI difficulty level.
		/// </summary>
		public int Depth => depth;

		/// <summary>
		/// Creates a new AI player with specified color and search depth.
		/// </summary>
		/// <param name="color">The color of pieces this AI will play (White or Black)</param>
		/// <param name="depth">
		/// How many moves ahead the AI will look (1-5).
		/// Higher values create stronger AI but exponentially increase computation time:
		/// - Depth 1: Very weak, only considers immediate moves
		/// - Depth 2: Weak, considers opponent's responses
		/// - Depth 3: Moderate, can see simple tactics
		/// - Depth 4: Strong, sees most tactical combinations
		/// - Depth 5: Very strong, approaches advanced play
		/// </param>
		public AIPlayer(PieceColor color, int depth = 3) : base(color)
		{
			// Ensure depth is reasonable (1-5)
			// Depths > 5 become very slow without additional optimizations
			// such as more sophisticated pruning or parallel search
			this.depth = Math.Max(1, Math.Min(5, depth));
		}

		/// <summary>
		/// Determines the best move for the AI player given the current board state.
		/// This is the main entry point called by the UI/game controller when it's the AI's turn.
		/// </summary>
		/// <param name="board">The current chess board state</param>
		/// <returns>The best move according to the AI's evaluation, or null if no legal moves exist</returns>
		public override Move GetMove(Board board)
		{
			// Start with a fresh transposition table for this move
			// This prevents the table from growing too large over a long game,
			// which would consume excessive memory and slow down hash lookups
			transpositionTable.Clear();

			// Initialize best move and score trackers
			Move bestMove = null;
			int bestScore = int.MinValue; // Start with worst possible score

			// Get all possible legal moves for the AI's color
			List<Move> possibleMoves = GetAllPossibleMoves(board, Color);

			// Pre-sort moves to improve alpha-beta pruning efficiency
			// By trying likely good moves first, pruning can eliminate more branches
			// This is a critical optimization that can improve search speed by orders of magnitude
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
				// Make a temporary move on the board to see what would happen
				board.MakeMove(move);

				// Evaluate the move using minimax algorithm
				// The 'false' parameter means we're switching to the opponent's perspective
				// (minimizing player) for the next level of the tree
				// We start with worst possible alpha/beta values to allow full search at root
				int score = Minimax(board, depth - 1, false, int.MinValue, int.MaxValue, Color);

				// Undo the move to restore the board to its previous state
				// This is essential for correct search tree traversal
				board.UndoMove();

				// Update best move if this one is better (higher score)
				if (score > bestScore)
				{
					bestScore = score;
					bestMove = move;
				}
				// For equal scores, randomly select to add variety to AI play
				// This prevents the AI from always making the same move in identical positions
				// which makes the game more interesting and less predictable
				else if (score == bestScore && random.Next(3) == 0)
				{
					bestMove = move;
				}

				// Update progress for UI and trigger the event
				// This provides visual feedback to the user about the AI's "thinking" process
				movesEvaluated++;
				OnThinkingProgress(new AIThinkingEventArgs(movesEvaluated * 100 / totalMovesToEvaluate));
			}

			// Final progress update to ensure UI shows 100% when finished
			OnThinkingProgress(new AIThinkingEventArgs(100));

			// Return the best move found
			return bestMove;
		}

		/// <summary>
		/// Raises the ThinkingProgress event with the current progress percentage.
		/// UI components can subscribe to this event to update visual indicators.
		/// Protected virtual to allow derived classes to override the event behavior.
		/// </summary>
		/// <param name="e">Event arguments containing progress percentage</param>
		protected virtual void OnThinkingProgress(AIThinkingEventArgs e)
		{
			// Null conditional operator ensures no exception if there are no subscribers
			ThinkingProgress?.Invoke(this, e);
		}

		/// <summary>
		/// Finds all possible legal moves for a specific color.
		/// This comprehensive search examines each piece of the specified color
		/// and determines all its legal destination squares.
		/// </summary>
		/// <param name="board">The current board state</param>
		/// <param name="color">The color to get moves for (White or Black)</param>
		/// <returns>A list of all legal moves for the specified color</returns>
		private List<Move> GetAllPossibleMoves(Board board, PieceColor color)
		{
			// Collection to store discovered legal moves
			List<Move> possibleMoves = new List<Move>();

			// Get all pieces of the specified color
			// Create a copy of the piece positions to avoid modification during enumeration
			// This prevents collection modification exceptions if the collection changes
			var piecePositionsCopy = board.GetPiecesByColor(color).ToList();

			// For each piece, find all its possible moves
			foreach (var entry in piecePositionsCopy)
			{
				Piece piece = entry.Key;
				Position pos = entry.Value;

				// Skip if piece is null or position is invalid
				// This is a defensive check to prevent exceptions
				if (piece == null || pos == null || pos.X < 0 || pos.X > 7 || pos.Y < 0 || pos.Y > 7)
					continue;

				// Try all possible destination squares on the board
				for (int toX = 0; toX < 8; toX++)
				{
					for (int toY = 0; toY < 8; toY++)
					{
						// Create a potential move to this destination
						Move move = new Move(pos.X, pos.Y, toX, toY, new DummyPlayer(color));

						// Check if the move is legal according to chess rules
						// The Board.IsValidMove method handles piece-specific movement rules,
						// captures, and checks for king safety
						if (board.IsValidMove(move))
						{
							possibleMoves.Add(move);
						}
					}
				}
			}

			// Return all discovered legal moves
			return possibleMoves;
		}

		/// <summary>
		/// Pre-sorts moves to improve alpha-beta pruning efficiency.
		/// Good moves are tried first, which increases the chances of finding good cutoffs.
		/// This is a key optimization that can make alpha-beta pruning orders of magnitude more efficient.
		/// </summary>
		/// <param name="board">The current board state</param>
		/// <param name="moves">List of possible moves</param>
		/// <returns>Sorted list of moves (best first) according to heuristic evaluation</returns>
		private List<Move> PreSortMoves(Board board, List<Move> moves)
		{
			// Use LINQ to sort moves by estimated quality
			return moves.OrderByDescending(move =>
			{
				int score = 0;

				// Prioritize captures - capturing valuable pieces is generally good
				Piece capturedPiece = board.GetPiece(move.ToX, move.ToY);
				if (capturedPiece != null)
				{
					// Capture value is based on the value of the captured piece
					// MVV-LVA (Most Valuable Victim - Least Valuable Aggressor)
					// It's better to capture valuable pieces with less valuable ones
					score += GetPieceValue(capturedPiece) - (GetPieceValue(board.GetPiece(move.FromX, move.FromY)) / 10);
				}

				// Prioritize center control for pawns
				// The center squares are strategically important in chess
				Piece piece = board.GetPiece(move.FromX, move.FromY);
				if (piece is Pawn)
				{
					// Calculate Manhattan distance from center (lower is better)
					// Center is defined as the 4 central squares (3,3), (3,4), (4,3), (4,4)
					int centerDistance = Math.Abs(move.ToX - 3) + Math.Abs(move.ToX - 4) +
										Math.Abs(move.ToY - 3) + Math.Abs(move.ToY - 4);
					score += (8 - centerDistance) * 5; // Higher bonus for moves closer to center
				}

				// Knights are particularly good on central squares
				// Chess theory emphasizes that knights are stronger toward the center
				if (piece is Knight)
				{
					// Bonus for knights near the center (2,2) to (5,5)
					if ((move.ToX >= 2 && move.ToX <= 5) && (move.ToY >= 2 && move.ToY <= 5))
					{
						score += 20;
					}
				}

				// Add some randomness to avoid predictable play
				// This small random factor helps prevent the AI from always
				// making the same move in identical positions
				score += random.Next(5);

				return score;
			}).ToList();
		}

		/// <summary>
		/// The Minimax algorithm with alpha-beta pruning.
		/// This recursively evaluates all possible moves to find the best one.
		/// 
		/// Alpha-Beta pruning works by maintaining two values:
		/// - Alpha: the minimum score that the maximizing player is guaranteed
		/// - Beta: the maximum score that the minimizing player is guaranteed
		/// 
		/// If a move is found that would cause beta <= alpha, that branch is pruned
		/// because the opponent would never allow that position to be reached.
		/// </summary>
		/// <param name="board">Current board state</param>
		/// <param name="depth">Remaining depth to search</param>
		/// <param name="maximizingPlayer">True if current player is maximizing, false if minimizing</param>
		/// <param name="alpha">Alpha value for pruning (best already found for maximizing player)</param>
		/// <param name="beta">Beta value for pruning (best already found for minimizing player)</param>
		/// <param name="maximizingColor">The color of the maximizing player (AI's color)</param>
		/// <returns>The best score for the current player</returns>
		private int Minimax(Board board, int depth, bool maximizingPlayer, int alpha, int beta, PieceColor maximizingColor)
		{
			// Generate a hash key for the current board position
			// This key uniquely identifies the position for the transposition table
			string boardKey = GenerateBoardKey(board, depth, maximizingPlayer);

			// Check transposition table first - if we've already evaluated this position, reuse the result
			// This is a critical optimization that can dramatically improve performance
			if (transpositionTable.ContainsKey(boardKey))
			{
				return transpositionTable[boardKey];
			}

			// Terminal conditions - stop recursion at leaf nodes

			// If maximum depth reached, use quiescence search for more accurate evaluation
			// This prevents the horizon effect by extending search in volatile positions
			if (depth == 0)
				return QuiescenceSearch(board, 3, alpha, beta, maximizingColor);

			// Game ending conditions with appropriate scores
			// Checkmate is the ultimate goal/disaster, so it gets an extreme score
			if (board.IsCheckmate(maximizingColor))
				return -10000; // Being checkmated is very bad for the AI

			if (board.IsCheckmate(maximizingColor == PieceColor.White ? PieceColor.Black : PieceColor.White))
				return 10000; // Checkmating opponent is very good for the AI

			if (board.IsStalemate())
				return 0; // Stalemate is neutral (draw)

			// Determine whose turn it is in this position
			PieceColor currentColor = maximizingPlayer ? maximizingColor :
									 (maximizingColor == PieceColor.White ? PieceColor.Black : PieceColor.White);

			// Get all possible moves for the current player
			List<Move> possibleMoves = GetAllPossibleMoves(board, currentColor);

			// If no moves are available, it's a terminal position (checkmate or stalemate)
			// Should be caught by the previous checks, but this is a safety net
			if (possibleMoves.Count == 0)
				return maximizingPlayer ? -9000 : 9000;

			// Order moves to improve alpha-beta pruning efficiency
			// By trying likely good moves first, we increase chances of early cutoffs
			possibleMoves = OrderMoves(board, possibleMoves);

			// Maximizing player's turn (AI is trying to maximize its score)
			if (maximizingPlayer)
			{
				int maxEval = int.MinValue; // Start with worst possible score

				foreach (Move move in possibleMoves)
				{
					// Make move, evaluate recursively, and undo
					board.MakeMove(move);
					int eval = Minimax(board, depth - 1, false, alpha, beta, maximizingColor);
					board.UndoMove();

					// Update best evaluation
					maxEval = Math.Max(maxEval, eval);

					// Update alpha value - the minimum score the maximizing player is guaranteed
					alpha = Math.Max(alpha, eval);

					// Beta cutoff - if this branch is worse than what the opponent can force,
					// we can stop evaluating this branch (pruning)
					// This is the key optimization of alpha-beta pruning
					if (beta <= alpha)
						break;
				}

				// Store result in transposition table for future reuse
				transpositionTable[boardKey] = maxEval;

				return maxEval;
			}
			// Minimizing player's turn (opponent is trying to minimize AI's score)
			else
			{
				int minEval = int.MaxValue; // Start with best possible score

				foreach (Move move in possibleMoves)
				{
					// Make move, evaluate recursively, and undo
					board.MakeMove(move);
					int eval = Minimax(board, depth - 1, true, alpha, beta, maximizingColor);
					board.UndoMove();

					// Update best evaluation
					minEval = Math.Min(minEval, eval);

					// Update beta value - the maximum score the minimizing player is guaranteed
					beta = Math.Min(beta, eval);

					// Alpha cutoff - if this branch is better than what the AI can force,
					// we can stop evaluating this branch (pruning)
					if (beta <= alpha)
						break;
				}

				// Store result in transposition table for future reuse
				transpositionTable[boardKey] = minEval;

				return minEval;
			}
		}

		/// <summary>
		/// Quiescence search to avoid horizon effect.
		/// 
		/// The "horizon effect" occurs when the AI evaluates a position right 
		/// before an important capture/exchange would happen, leading to inaccurate evaluations.
		/// Quiescence search continues evaluating captures beyond the regular search depth
		/// to ensure the position is "quiet" (stable) before evaluating.
		/// 
		/// This is an essential refinement to the basic Minimax algorithm that significantly
		/// improves playing strength, particularly in tactical positions.
		/// </summary>
		/// <param name="board">Current board state</param>
		/// <param name="depth">Remaining quiescence depth</param>
		/// <param name="alpha">Alpha value for pruning</param>
		/// <param name="beta">Beta value for pruning</param>
		/// <param name="maximizingColor">The color of the maximizing player</param>
		/// <returns>The best score for the current player in a quiet position</returns>
		private int QuiescenceSearch(Board board, int depth, int alpha, int beta, PieceColor maximizingColor)
		{
			// Get static evaluation of the current position
			// This is the "stand pat" score - what if we do nothing?
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
			// This focuses on resolving immediate tactical situations only
			List<Move> captureMoves = GetCaptureMoves(board, maximizingColor);

			// Order captures by value for more efficient pruning
			captureMoves = OrderMoves(board, captureMoves);

			// Try each capture move
			foreach (Move move in captureMoves)
			{
				board.MakeMove(move);

				// Recursive call with colors swapped
				// The negative sign flips the perspective between players
				int score = -QuiescenceSearch(board, depth - 1, -beta, -alpha,
					maximizingColor == PieceColor.White ? PieceColor.Black : PieceColor.White);

				board.UndoMove();

				// Update alpha and check for beta cutoff
				if (score >= beta)
					return beta;
				if (score > alpha)
					alpha = score;
			}

			// Return the best score found
			return alpha;
		}

		/// <summary>
		/// Gets only capture moves for the quiescence search.
		/// This focuses the quiescence search on resolving immediate tactical situations
		/// without exploring the entire move tree.
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
						// Only consider moves that capture a piece of the opposite color
						Piece targetPiece = board.GetPiece(toX, toY);
						if (targetPiece != null && targetPiece.Color != color)
						{
							// Create the potential capturing move
							Move move = new Move(pos.X, pos.Y, toX, toY, new DummyPlayer(color));

							// Only add legal captures
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
		/// Generates a unique key for the current board position.
		/// This is used for the transposition table to efficiently cache and retrieve positions.
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

			// Create a string representation of the board
			for (int y = 0; y < 8; y++)
			{
				for (int x = 0; x < 8; x++)
				{
					Piece piece = board.GetPiece(x, y);
					if (piece == null)
						key.Append("-"); // Empty square
					else
						key.Append(piece.Symbol); // Piece symbol (e.g., "P" for white pawn)
				}
			}

			// Include depth and player in the key since evaluations differ by depth and turn
			// This is important because the same position at different depths might have
			// different evaluations due to the horizon effect
			key.Append("_").Append(depth).Append("_").Append(maximizingPlayer ? "1" : "0");

			return key.ToString();
		}

		/// <summary>
		/// Orders moves based on their likely quality to improve alpha-beta pruning.
		/// This is a more sophisticated version of PreSortMoves used during the deep search,
		/// taking into account additional factors like check.
		/// </summary>
		/// <param name="board">Current board state</param>
		/// <param name="moves">List of possible moves</param>
		/// <returns>Sorted list of moves (best first)</returns>
		private List<Move> OrderMoves(Board board, List<Move> moves)
		{
			// Score moves for better alpha-beta pruning
			// Captures and checks are tried first as they're often stronger
			return moves.OrderByDescending(move =>
			{
				int score = 0;

				// Prioritize captures based on MVV-LVA (Most Valuable Victim - Least Valuable Aggressor)
				// This chess heuristic suggests capturing valuable pieces with less valuable ones
				Piece capturedPiece = board.GetPiece(move.ToX, move.ToY);
				if (capturedPiece != null)
				{
					// More valuable for capturing valuable pieces with less valuable pieces
					score += GetPieceValue(capturedPiece) - GetPieceValue(board.GetPiece(move.FromX, move.FromY)) / 10;
				}

				// Check if move gives check (bonus for this)
				// Checking the opponent often restricts their options and can lead to tactical advantages
				board.MakeMove(move);

				// Get the moved piece
				Piece movedPiece = board.GetPiece(move.ToX, move.ToY);

				// Only check for check if we have a valid piece
				if (movedPiece != null)
				{
					PieceColor opponentColor = movedPiece.Color == PieceColor.White ?
										   PieceColor.Black : PieceColor.White;

					// Bonus points for putting opponent in check
					// Checks are often strong moves that limit opponent's options
					if (board.IsInCheck(opponentColor))
					{
						score += 50;
					}
				}

				board.UndoMove();

				// Bonus for advancing pawns toward promotion
				// Pawn promotion is a powerful tactical resource
				Piece piece = board.GetPiece(move.FromX, move.FromY);
				if (piece is Pawn)
				{
					// White pawns want to move toward row 0, black toward row 7
					// This follows chess notation where white pawns advance from rank 7 to 0
					int promotionRow = piece.Color == PieceColor.White ? 0 : 7;
					int distanceToPromotion = Math.Abs(move.ToY - promotionRow);
					score += (7 - distanceToPromotion) * 10; // Higher bonus for pawns closer to promotion
				}

				return score;
			}).ToList();
		}

		/// <summary>
		/// Returns the material value of a chess piece.
		/// These values are standard in chess programming and represent 
		/// the approximate relative strength of each piece type.
		/// </summary>
		/// <param name="piece">The piece to evaluate</param>
		/// <returns>The value of the piece in centipawns (1/100th of a pawn)</returns>
		private int GetPieceValue(Piece piece)
		{
			if (piece == null) return 0;

			// Standard piece values (centipawns)
			// These values are widely accepted in chess theory
			if (piece is Pawn) return 100;      // 1.0 pawns
			if (piece is Knight) return 320;    // 3.2 pawns
			if (piece is Bishop) return 330;    // 3.3 pawns (slightly better than knight)
			if (piece is Rook) return 500;      // 5.0 pawns
			if (piece is Queen) return 900;     // 9.0 pawns
			if (piece is King) return 20000;    // High value to ensure king safety

			return 0; // Unknown piece type
		}

		/// <summary>
		/// Asynchronous version of GetMove that can be called from UI without freezing.
		/// This allows the game to remain responsive while the AI is "thinking".
		/// The UI can update animations, progress indicators, or allow user interactions.
		/// </summary>
		/// <param name="board">Current board state</param>
		/// <param name="cancellationToken">Optional token to cancel operation</param>
		/// <returns>Task containing the best move</returns>
		public async Task<Move> GetMoveAsync(Board board, CancellationToken cancellationToken = default)
		{
			// Run the AI calculation on a background thread
			// This prevents the UI thread from freezing during calculation
			return await Task.Run(() => GetMove(board), cancellationToken);
		}
	}
}