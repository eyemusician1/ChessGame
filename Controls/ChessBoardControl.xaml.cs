using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ChessGame.Models;
using System.Linq;

namespace ChessGame.Controls
{
	/// <summary>
	/// Interaction logic for ChessBoardControl.xaml
	/// </summary>
	public partial class ChessBoardControl : UserControl
	{
		private Game game;
		private Rectangle[,] squares;
		private Dictionary<string, UIElement> pieceElements; // Track by position
		private Position selectedPosition;
		private List<Position> validMovePositions;
		private Move lastMove;
		private bool isInitialized = false;

		// Events
		public event EventHandler<MoveEventArgs> MoveCompleted;
		public event EventHandler<AIThinkingEventArgs> AIThinking;

		// Board colors that complement the pieces better
		private static readonly SolidColorBrush LightSquareBrush = new SolidColorBrush(Color.FromRgb(240, 217, 181)); // Warm beige
		private static readonly SolidColorBrush DarkSquareBrush = new SolidColorBrush(Color.FromRgb(181, 136, 99));   // Walnut brown

		// Cache for performance
		private readonly Dictionary<string, string> pieceSymbolCache = new Dictionary<string, string>();
		private readonly Dictionary<PieceColor, Brush> pieceBrushCache = new Dictionary<PieceColor, Brush>();

		public ChessBoardControl()
		{
			InitializeComponent();

			squares = new Rectangle[8, 8];
			pieceElements = new Dictionary<string, UIElement>();
			validMovePositions = new List<Position>();

			// Initialize caches for better performance
			pieceBrushCache[PieceColor.White] = Brushes.White;
			pieceBrushCache[PieceColor.Black] = Brushes.Black;

			// Create the chess board squares
			CreateBoardSquares();

			// Handle size changes
			SizeChanged += ChessBoardControl_SizeChanged;
		}

		public void Initialize(Game game)
		{
			// Unsubscribe from previous events to prevent memory leaks
			if (this.game != null)
			{
				this.game.BoardUpdated -= Game_BoardUpdated;

				// Find and unsubscribe from any AI players
				foreach (var player in new[] { this.game.CurrentPlayer, this.game.BlackPlayer })
				{
					if (player is AIPlayer aiPlayer)
					{
						aiPlayer.ThinkingProgress -= AIPlayer_ThinkingProgress;
					}
				}
			}

			// Set the new game
			this.game = game;

			// Subscribe to game events
			game.BoardUpdated += Game_BoardUpdated;

			// Subscribe to AI thinking events if any player is an AI
			foreach (var player in new[] { game.CurrentPlayer, game.BlackPlayer })
			{
				if (player is AIPlayer aiPlayer)
				{
					aiPlayer.ThinkingProgress += AIPlayer_ThinkingProgress;
				}
			}

			// Clear any existing pieces
			PiecesCanvas.Children.Clear();
			pieceElements.Clear();

			// Clear highlights
			HighlightCanvas.Children.Clear();
			MoveIndicatorCanvas.Children.Clear();

			// Reset selection
			selectedPosition = null;
			validMovePositions.Clear();
			lastMove = null;

			// Update the board
			UpdateBoard();

			isInitialized = true;
		}

		private void AIPlayer_ThinkingProgress(object sender, AIThinkingEventArgs e)
		{
			AIThinking?.Invoke(this, e);
		}

		private void Game_BoardUpdated(object sender, BoardUpdatedEventArgs e)
		{
			// Update the board on the UI thread
			Dispatcher.InvokeAsync(() => UpdateBoard());
		}

		private void CreateBoardSquares()
		{
			// Clear existing squares
			BoardGrid.Children.Clear();

			// Fixed size for the board
			double squareSize = 50;

			for (int row = 0; row < 8; row++)
			{
				for (int col = 0; col < 8; col++)
				{
					Rectangle square = new Rectangle();
					square.Width = squareSize;
					square.Height = squareSize;

					// Use warmer colors that complement the pieces better
					square.Fill = ((row + col) % 2 == 0) ? LightSquareBrush : DarkSquareBrush;

					// Store the position in the Tag property (chess coordinates)
					// In chess, row 0 is at the bottom, but in UI row 0 is at the top
					int chessRow = 7 - row;
					int chessCol = col;
					square.Tag = new Position(chessCol, chessRow);

					// Add mouse event handlers
					square.MouseDown += Square_MouseDown;

					// Add to the grid
					BoardGrid.Children.Add(square);

					// Store in the array
					squares[chessCol, chessRow] = square;
				}
			}
		}

		private void Square_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (game == null || !(game.CurrentPlayer is HumanPlayer))
				return;

			Rectangle square = (Rectangle)sender;
			Position position = (Position)square.Tag;

			// If a piece is already selected
			if (selectedPosition != null)
			{
				// If the same square is clicked again, deselect it
				if (selectedPosition.X == position.X && selectedPosition.Y == position.Y)
				{
					ClearHighlights();
					selectedPosition = null;
					return;
				}

				// Check if the clicked square is a valid move
				if (validMovePositions.Contains(position))
				{
					// Create a move
					Move move = new Move(
						selectedPosition.X, selectedPosition.Y,
						position.X, position.Y,
						game.CurrentPlayer);

					// Try to make the move
					if (game.MakeUIMove(move))
					{
						// Store the last move for highlighting
						lastMove = move;

						// Notify listeners
						MoveCompleted?.Invoke(this, new MoveEventArgs(move));
					}

					// Clear selection and highlights
					ClearHighlights();
					selectedPosition = null;
				}
				else
				{
					// Check if the clicked square has a piece of the current player
					Piece piece = game.GetPieceAt(position.X, position.Y);
					if (piece != null && piece.Color == game.CurrentPlayer.Color)
					{
						// Select this piece instead
						selectedPosition = position;
						HighlightSelectedSquare();
						HighlightValidMoves();
					}
					else
					{
						// Clear selection and highlights
						ClearHighlights();
						selectedPosition = null;
					}
				}
			}
			else
			{
				// Check if the clicked square has a piece of the current player
				Piece piece = game.GetPieceAt(position.X, position.Y);
				if (piece != null && piece.Color == game.CurrentPlayer.Color)
				{
					// Select this piece
					selectedPosition = position;
					HighlightSelectedSquare();
					HighlightValidMoves();
				}
			}
		}

		private void HighlightSelectedSquare()
		{
			if (selectedPosition == null)
				return;

			// Create a highlight rectangle
			Rectangle highlight = new Rectangle();
			highlight.Width = 50;
			highlight.Height = 50;
			highlight.Fill = new SolidColorBrush(Color.FromArgb(178, 126, 196, 207)); // Selected square color

			// Position the highlight
			Canvas.SetLeft(highlight, selectedPosition.X * 50);
			Canvas.SetTop(highlight, (7 - selectedPosition.Y) * 50);

			// Add to the canvas
			HighlightCanvas.Children.Add(highlight);
		}

		private void HighlightValidMoves()
		{
			if (selectedPosition == null)
				return;

			// Clear previous valid moves
			validMovePositions.Clear();

			// Find all valid moves for the selected piece
			for (int toY = 0; toY < 8; toY++)
			{
				for (int toX = 0; toX < 8; toX++)
				{
					Move move = new Move(
						selectedPosition.X, selectedPosition.Y,
						toX, toY,
						game.CurrentPlayer);

					if (game.IsValidMove(move))
					{
						validMovePositions.Add(new Position(toX, toY));

						// Create a highlight for this valid move
						Ellipse highlight = new Ellipse();
						double squareSize = 50;

						// If the target square has a piece, show a capture indicator
						Piece targetPiece = game.GetPieceAt(toX, toY);
						if (targetPiece != null)
						{
							highlight.Width = squareSize * 0.8;
							highlight.Height = squareSize * 0.8;
							highlight.Stroke = Brushes.White;
							highlight.StrokeThickness = 2;
							highlight.Fill = new SolidColorBrush(Color.FromArgb(128, 126, 196, 207)); // Valid move color
						}
						else
						{
							// Empty square indicator
							highlight.Width = squareSize * 0.3;
							highlight.Height = squareSize * 0.3;
							highlight.Fill = new SolidColorBrush(Color.FromArgb(128, 126, 196, 207)); // Valid move color
						}

						// Center the highlight in the square
						Canvas.SetLeft(highlight, toX * squareSize + (squareSize - highlight.Width) / 2);
						Canvas.SetTop(highlight, (7 - toY) * squareSize + (squareSize - highlight.Height) / 2);

						// Add to the canvas
						MoveIndicatorCanvas.Children.Add(highlight);
					}
				}
			}
		}

		private void HighlightLastMove()
		{
			if (lastMove == null)
				return;

			// Highlight the from square
			Rectangle fromHighlight = new Rectangle();
			fromHighlight.Width = 50;
			fromHighlight.Height = 50;
			fromHighlight.Fill = new SolidColorBrush(Color.FromArgb(77, 255, 235, 59)); // Last move color

			Canvas.SetLeft(fromHighlight, lastMove.FromX * 50);
			Canvas.SetTop(fromHighlight, (7 - lastMove.FromY) * 50);

			// Highlight the to square
			Rectangle toHighlight = new Rectangle();
			toHighlight.Width = 50;
			toHighlight.Height = 50;
			toHighlight.Fill = new SolidColorBrush(Color.FromArgb(77, 255, 235, 59)); // Last move color

			Canvas.SetLeft(toHighlight, lastMove.ToX * 50);
			Canvas.SetTop(toHighlight, (7 - lastMove.ToY) * 50);

			// Add to the canvas
			HighlightCanvas.Children.Add(fromHighlight);
			HighlightCanvas.Children.Add(toHighlight);
		}

		private void ClearHighlights()
		{
			HighlightCanvas.Children.Clear();
			MoveIndicatorCanvas.Children.Clear();

			// Re-highlight the last move
			HighlightLastMove();
		}

		private void UpdateBoard()
		{
			if (game == null)
				return;

			// Clear highlights
			ClearHighlights();

			// Update pieces - completely rebuild the pieces display
			RebuildPieces();

			// Highlight the last move
			HighlightLastMove();
		}

		// Enhance the RebuildPieces method to completely rebuild from scratch
		private void RebuildPieces()
		{
			if (game == null)
				return;

			// Clear all existing pieces
			PiecesCanvas.Children.Clear();
			pieceElements.Clear();

			// Add all pieces currently on the board
			for (int y = 0; y < 8; y++)
			{
				for (int x = 0; x < 8; x++)
				{
					Piece piece = game.GetPieceAt(x, y);
					if (piece != null)
					{
						// Create a new piece element
						UIElement element = CreatePieceElement(piece);
						double squareSize = 50;

						Canvas.SetLeft(element, x * squareSize);
						Canvas.SetTop(element, (7 - y) * squareSize);

						// Add to canvas
						PiecesCanvas.Children.Add(element);

						// Store in dictionary with position as key - use position key to avoid duplicates
						string posKey = $"{x},{y}";
						pieceElements[posKey] = element;
					}
				}
			}
		}

		// Create a piece element with better fallback handling
		private UIElement CreatePieceElement(Piece piece)
		{
			double squareSize = 50;

			// Get cached symbol or create new one
			string pieceKey = $"{piece.GetType().Name}_{piece.Color}";
			if (!pieceSymbolCache.TryGetValue(pieceKey, out string symbol))
			{
				symbol = GetPieceSymbol(piece);
				pieceSymbolCache[pieceKey] = symbol;
			}

			// Get cached brush
			Brush foreground = pieceBrushCache[piece.Color];

			// Create a text representation with Unicode chess symbols
			TextBlock textBlock = new TextBlock
			{
				Text = symbol,
				FontFamily = new FontFamily("Segoe UI Symbol"),
				FontSize = 36, // Fixed larger font size for better visibility
				FontWeight = FontWeights.Bold,
				Foreground = foreground,
				TextAlignment = TextAlignment.Center,
				Width = squareSize,
				Height = squareSize,
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Center
			};

			// Center the text in a container
			Grid container = new Grid
			{
				Width = squareSize,
				Height = squareSize,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};

			container.Children.Add(textBlock);
			return container;
		}

		private string GetPieceSymbol(Piece piece)
		{
			if (piece is King) return piece.Color == PieceColor.White ? "♔" : "♚";
			if (piece is Queen) return piece.Color == PieceColor.White ? "♕" : "♛";
			if (piece is Rook) return piece.Color == PieceColor.White ? "♖" : "♜";
			if (piece is Bishop) return piece.Color == PieceColor.White ? "♗" : "♝";
			if (piece is Knight) return piece.Color == PieceColor.White ? "♘" : "♞";
			if (piece is Pawn) return piece.Color == PieceColor.White ? "♙" : "♟";
			return "?";
		}

		private void ChessBoardControl_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			// No need to resize with Viewbox
		}
	}

	public class MoveEventArgs : EventArgs
	{
		public Move Move { get; }

		public MoveEventArgs(Move move)
		{
			Move = move;
		}
	}
}

