// Models/Game.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChessGame.Models
{
	/// <summary>
	/// Represents the state of a chess game.
	/// </summary>
	public enum GameState
	{
		/// <summary>
		/// The game is in progress.
		/// </summary>
		InProgress,

		/// <summary>
		/// White has won by checkmate.
		/// </summary>
		WhiteWinsByCheckmate,

		/// <summary>
		/// Black has won by checkmate.
		/// </summary>
		BlackWinsByCheckmate,

		/// <summary>
		/// The game is a draw by stalemate.
		/// </summary>
		DrawByStalemate,

		/// <summary>
		/// The game is a draw by insufficient material.
		/// </summary>
		DrawByInsufficientMaterial,

		/// <summary>
		/// The game is a draw by the fifty-move rule.
		/// </summary>
		DrawByFiftyMoveRule,

		/// <summary>
		/// The game is a draw by threefold repetition.
		/// </summary>
		DrawByRepetition
	}

	/// <summary>
	/// Event arguments for game state changes.
	/// </summary>
	public class GameStateChangedEventArgs : EventArgs
	{
		/// <summary>
		/// Gets the new game state.
		/// </summary>
		public GameState State { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="GameStateChangedEventArgs"/> class.
		/// </summary>
		/// <param name="state">The new game state.</param>
		public GameStateChangedEventArgs(GameState state)
		{
			State = state;
		}
	}

	/// <summary>
	/// Game controller class
	/// </summary>
	public class Game
	{
		private Board board;
		private Player whitePlayer;
		private Player blackPlayer;
		private Player currentPlayer;
		private List<Move> moveHistory;
		private int halfMoveClock; // For fifty-move rule
		private Dictionary<string, int> positionHistory; // For threefold repetition

		/// <summary>
		/// Gets the current state of the game.
		/// </summary>
		public GameState State { get; private set; }

		/// <summary>
		/// Gets the current player.
		/// </summary>
		public Player CurrentPlayer => currentPlayer;

		/// <summary>
		/// Gets the current move number (starts at 1).
		/// </summary>
		public int MoveNumber => (moveHistory.Count / 2) + 1;

		/// <summary>
		/// Gets the move history in PGN format.
		/// </summary>
		public string PgnMoveHistory => GeneratePgnMoveHistory();

		/// <summary>
		/// Event raised when the board is updated.
		/// </summary>
		public event EventHandler<BoardUpdatedEventArgs> BoardUpdated;

		/// <summary>
		/// Event raised when the game state changes.
		/// </summary>
		public event EventHandler<GameStateChangedEventArgs> GameStateChanged;

		/// <summary>
		/// Initializes a new instance of the <see cref="Game"/> class.
		/// </summary>
		public Game()
		{
			Initialize(3);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Game"/> class with a specified AI difficulty.
		/// </summary>
		/// <param name="aiDepth">The search depth for the AI player.</param>
		public Game(int aiDepth = 3)
		{
			Initialize(aiDepth);
		}

		/// <summary>
		/// Initializes the game with the specified AI difficulty.
		/// </summary>
		/// <param name="aiDepth">The search depth for the AI player.</param>
		private void Initialize(int aiDepth)
		{
			// Create a new board to ensure clean state
			board = new Board();

			// Create new player instances
			whitePlayer = new HumanPlayer(PieceColor.White);
			blackPlayer = new AIPlayer(PieceColor.Black, aiDepth);

			currentPlayer = whitePlayer; // White goes first
			State = GameState.InProgress;
			moveHistory = new List<Move>();
			halfMoveClock = 0;
			positionHistory = new Dictionary<string, int>();
		}

		/// <summary>
		/// Starts a new game.
		/// </summary>
		public void Start()
		{
			board.Initialize();
			currentPlayer = whitePlayer;
			State = GameState.InProgress;
			moveHistory.Clear();
			halfMoveClock = 0;
			positionHistory.Clear();

			// Add initial position to history
			string initialPosition = GetBoardFen();
			positionHistory[initialPosition] = 1;

			// Notify listeners
			OnBoardUpdated(new BoardUpdatedEventArgs(board));
			OnGameStateChanged(new GameStateChangedEventArgs(State));
		}

		/// <summary>
		/// Makes a move from algebraic notation.
		/// </summary>
		/// <param name="from">The starting square (e.g., "e2").</param>
		/// <param name="to">The destination square (e.g., "e4").</param>
		/// <returns>True if the move was valid and made; otherwise, false.</returns>
		public bool MakeUIMove(string from, string to)
		{
			// Convert algebraic notation to board coordinates
			int fromX = from[0] - 'a';
			int fromY = from[1] - '1';
			int toX = to[0] - 'a';
			int toY = to[1] - '1';

			Move move = new Move(fromX, fromY, toX, toY, currentPlayer);

			return MakeUIMove(move);
		}

		/// <summary>
		/// Makes a move from the UI.
		/// </summary>
		/// <param name="move">The move to make.</param>
		/// <returns>True if the move was valid and made; otherwise, false.</returns>
		public bool MakeUIMove(Move move)
		{
			if (State != GameState.InProgress)
				return false;

			if (!board.IsValidMove(move))
				return false;

			// Make the move
			bool captureOrPawnMove = MakeMoveInternal(move);

			// Update fifty-move rule counter
			if (captureOrPawnMove)
				halfMoveClock = 0;
			else
				halfMoveClock++;

			// Update position history for threefold repetition
			string position = GetBoardFen();
			if (!positionHistory.ContainsKey(position))
				positionHistory[position] = 1;
			else
				positionHistory[position]++;

			// Check for game end conditions
			UpdateGameState();

			// Notify listeners
			OnBoardUpdated(new BoardUpdatedEventArgs(board));

			if (State != GameState.InProgress)
			{
				OnGameStateChanged(new GameStateChangedEventArgs(State));
				return true;
			}

			// Switch players
			currentPlayer = currentPlayer == whitePlayer ? blackPlayer : whitePlayer;

			// If it's AI's turn, make AI move
			if (currentPlayer is AIPlayer aiPlayer)
			{
				// Subscribe to AI thinking progress
				aiPlayer.ThinkingProgress += (sender, e) =>
				{
					OnBoardUpdated(new BoardUpdatedEventArgs(board, $"AI thinking... {e.ProgressPercentage}%"));
				};

				Move aiMove = aiPlayer.GetMove(board);
				if (aiMove != null)
				{
					MakeUIMove(aiMove);
				}
			}

			return true;
		}

		/// <summary>
		/// Makes a move and returns whether it was a capture or pawn move.
		/// </summary>
		/// <param name="move">The move to make.</param>
		/// <returns>True if the move was a capture or pawn move; otherwise, false.</returns>
		private bool MakeMoveInternal(Move move)
		{
			Piece piece = board.GetPiece(move.FromX, move.FromY);
			Piece capturedPiece = board.GetPiece(move.ToX, move.ToY);

			// Make the move on the board
			board.MakeMove(move);

			// Add to history
			moveHistory.Add(move);

			// Return true if it was a capture or pawn move (resets fifty-move rule)
			return capturedPiece != null || piece is Pawn;
		}

		/// <summary>
		/// Updates the game state based on the current board position.
		/// </summary>
		private void UpdateGameState()
		{
			// Check for checkmate
			if (board.IsCheckmate(PieceColor.White))
			{
				State = GameState.BlackWinsByCheckmate;
				return;
			}

			if (board.IsCheckmate(PieceColor.Black))
			{
				State = GameState.WhiteWinsByCheckmate;
				return;
			}

			// Check for stalemate
			if (board.IsStalemate())
			{
				State = GameState.DrawByStalemate;
				return;
			}

			// Check for insufficient material
			if (HasInsufficientMaterial())
			{
				State = GameState.DrawByInsufficientMaterial;
				return;
			}

			// Check for fifty-move rule
			if (halfMoveClock >= 100) // 50 full moves = 100 half moves
			{
				State = GameState.DrawByFiftyMoveRule;
				return;
			}

			// Check for threefold repetition
			if (positionHistory.ContainsKey(GetBoardFen()) && positionHistory[GetBoardFen()] >= 3)
			{
				State = GameState.DrawByRepetition;
				return;
			}
		}

		/// <summary>
		/// Checks if the board has insufficient material for checkmate.
		/// </summary>
		/// <returns>True if there is insufficient material; otherwise, false.</returns>
		private bool HasInsufficientMaterial()
		{
			Dictionary<Piece, Position> whitePieces = board.GetPiecesByColor(PieceColor.White);
			Dictionary<Piece, Position> blackPieces = board.GetPiecesByColor(PieceColor.Black);

			// King vs King
			if (whitePieces.Count == 1 && blackPieces.Count == 1)
				return true;

			// King + Knight/Bishop vs King
			if ((whitePieces.Count == 2 && blackPieces.Count == 1) ||
				(whitePieces.Count == 1 && blackPieces.Count == 2))
			{
				bool whiteHasMinorPiece = whitePieces.Keys.Any(p => p is Knight || p is Bishop);
				bool blackHasMinorPiece = blackPieces.Keys.Any(p => p is Knight || p is Bishop);

				return whiteHasMinorPiece || blackHasMinorPiece;
			}

			// King + Bishop vs King + Bishop (same color bishops)
			if (whitePieces.Count == 2 && blackPieces.Count == 2)
			{
				Bishop whiteBishop = whitePieces.Keys.OfType<Bishop>().FirstOrDefault();
				Bishop blackBishop = blackPieces.Keys.OfType<Bishop>().FirstOrDefault();

				if (whiteBishop != null && blackBishop != null)
				{
					// Check if bishops are on same colored squares
					Position whitePos = whitePieces[whiteBishop];
					Position blackPos = blackPieces[blackBishop];

					bool whiteSquareColor = (whitePos.X + whitePos.Y) % 2 == 0;
					bool blackSquareColor = (blackPos.X + blackPos.Y) % 2 == 0;

					return whiteSquareColor == blackSquareColor;
				}
			}

			return false;
		}

		/// <summary>
		/// Gets the current board position in Forsyth-Edwards Notation (FEN).
		/// </summary>
		/// <returns>The FEN string representing the current position.</returns>
		private string GetBoardFen()
		{
			StringBuilder fen = new StringBuilder();

			// Board position
			for (int y = 7; y >= 0; y--)
			{
				int emptyCount = 0;

				for (int x = 0; x < 8; x++)
				{
					Piece piece = board.GetPiece(x, y);

					if (piece == null)
					{
						emptyCount++;
					}
					else
					{
						if (emptyCount > 0)
						{
							fen.Append(emptyCount);
							emptyCount = 0;
						}

						fen.Append(piece.Symbol);
					}
				}

				if (emptyCount > 0)
					fen.Append(emptyCount);

				if (y > 0)
					fen.Append('/');
			}

			return fen.ToString();
		}

		/// <summary>
		/// Generates the move history in PGN (Portable Game Notation) format.
		/// </summary>
		/// <returns>The PGN move history string.</returns>
		private string GeneratePgnMoveHistory()
		{
			StringBuilder pgn = new StringBuilder();

			for (int i = 0; i < moveHistory.Count; i++)
			{
				if (i % 2 == 0)
				{
					pgn.Append((i / 2) + 1);
					pgn.Append(". ");
				}

				pgn.Append(GetAlgebraicNotation(moveHistory[i]));
				pgn.Append(" ");
			}

			// Add result
			switch (State)
			{
				case GameState.WhiteWinsByCheckmate:
					pgn.Append("1-0");
					break;
				case GameState.BlackWinsByCheckmate:
					pgn.Append("0-1");
					break;
				case GameState.DrawByStalemate:
				case GameState.DrawByInsufficientMaterial:
				case GameState.DrawByFiftyMoveRule:
				case GameState.DrawByRepetition:
					pgn.Append("1/2-1/2");
					break;
				default:
					pgn.Append("*"); // Game in progress
					break;
			}

			return pgn.ToString();
		}

		/// <summary>
		/// Converts a move to algebraic notation.
		/// </summary>
		/// <param name="move">The move to convert.</param>
		/// <returns>The move in algebraic notation.</returns>
		private string GetAlgebraicNotation(Move move)
		{
			char fromFile = (char)('a' + move.FromX);
			char fromRank = (char)('1' + move.FromY);
			char toFile = (char)('a' + move.ToX);
			char toRank = (char)('1' + move.ToY);

			return $"{fromFile}{fromRank}-{toFile}{toRank}";
		}

		/// <summary>
		/// Method to get AI move for UI preview.
		/// </summary>
		/// <returns>A string representation of the suggested move.</returns>
		public string GetAIHint()
		{
			AIPlayer hintPlayer = new AIPlayer(currentPlayer.Color, 2);
			Move hintMove = hintPlayer.GetMove(board);
			if (hintMove != null)
			{
				char fromFile = (char)('a' + hintMove.FromX);
				char fromRank = (char)('1' + hintMove.FromY);
				char toFile = (char)('a' + hintMove.ToX);
				char toRank = (char)('1' + hintMove.ToY);

				return $"{fromFile}{fromRank} to {toFile}{toRank}";
			}

			return "No hint available";
		}

		/// <summary>
		/// Method to undo the last move.
		/// </summary>
		/// <returns>True if a move was undone; otherwise, false.</returns>
		public bool UndoMove()
		{
			if (moveHistory.Count >= 2) // Undo both player and AI moves
			{
				// Undo last two moves
				board.UndoMove(moveHistory[moveHistory.Count - 1]);
				board.UndoMove(moveHistory[moveHistory.Count - 2]);

				// Remove from history
				moveHistory.RemoveAt(moveHistory.Count - 1);
				moveHistory.RemoveAt(moveHistory.Count - 1);

				// Update position history
				string position = GetBoardFen();
				if (positionHistory.ContainsKey(position))
				{
					positionHistory[position]--;
					if (positionHistory[position] <= 0)
						positionHistory.Remove(position);
				}

				// Reset game state
				State = GameState.InProgress;

				// Notify UI of board update
				OnBoardUpdated(new BoardUpdatedEventArgs(board));
				OnGameStateChanged(new GameStateChangedEventArgs(State));

				return true;
			}

			return false;
		}

		/// <summary>
		/// Get a piece at a specific position.
		/// </summary>
		/// <param name="x">The x-coordinate (0-7).</param>
		/// <param name="y">The y-coordinate (0-7).</param>
		/// <returns>The piece at the specified position, or null if empty.</returns>
		public Piece GetPieceAt(int x, int y)
		{
			return board.GetPiece(x, y);
		}

		/// <summary>
		/// Check if a move is valid.
		/// </summary>
		/// <param name="move">The move to check.</param>
		/// <returns>True if the move is valid; otherwise, false.</returns>
		public bool IsValidMove(Move move)
		{
			return board.IsValidMove(move);
		}

		/// <summary>
		/// Raises the BoardUpdated event.
		/// </summary>
		/// <param name="e">The event arguments.</param>
		protected virtual void OnBoardUpdated(BoardUpdatedEventArgs e)
		{
			BoardUpdated?.Invoke(this, e);
		}

		/// <summary>
		/// Raises the GameStateChanged event.
		/// </summary>
		/// <param name="e">The event arguments.</param>
		protected virtual void OnGameStateChanged(GameStateChangedEventArgs e)
		{
			GameStateChanged?.Invoke(this, e);
		}
	}
}

