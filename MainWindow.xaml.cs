using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ChessGame.Models;
using ChessGame.Controls;
using System.Windows.Threading;

namespace ChessGame
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private Game game;
		private bool isInitializing = false;

		public MainWindow()
		{
			InitializeComponent();

			// Initialize the game
			InitializeGame();

			// Subscribe to events
			ChessBoard.MoveCompleted += ChessBoard_MoveCompleted;
			ChessBoard.AIThinking += ChessBoard_AIThinking;

			// Prevent multiple initializations when slider value changes during initialization
			AIDifficultySlider.ValueChanged += AIDifficultySlider_ValueChanged;
		}

		private void InitializeGame()
		{
			if (isInitializing) return;

			isInitializing = true;

			try
			{
				// Clean up old game resources if they exist
				if (game != null)
				{
					game.BoardUpdated -= Game_BoardUpdated;
					game.GameStateChanged -= Game_GameStateChanged;

					// Explicitly dispose of any AI player resources
					if (game.CurrentPlayer is AIPlayer aiPlayer)
					{
						aiPlayer.ThinkingProgress -= AIPlayer_ThinkingProgress;
					}
				}

				// Get AI difficulty from slider
				int aiDepth = (int)AIDifficultySlider.Value;

				// Create a new game
				game = new Game(aiDepth);

				// Subscribe to game events
				game.BoardUpdated += Game_BoardUpdated;
				game.GameStateChanged += Game_GameStateChanged;

				// Start the game
				game.Start();

				// Initialize the chess board
				ChessBoard.Initialize(game);

				// Update UI
				UpdateGameInfo();

				// Clear move history
				MoveHistoryListBox.Items.Clear();
			}
			finally
			{
				isInitializing = false;
			}
		}

		private void AIPlayer_ThinkingProgress(object sender, AIThinkingEventArgs e)
		{
			// This is needed to properly unsubscribe from the event
		}

		private void Game_BoardUpdated(object sender, BoardUpdatedEventArgs e)
		{
			// Update on UI thread
			Dispatcher.Invoke(() => UpdateGameInfo());
		}

		private void Game_GameStateChanged(object sender, GameStateChangedEventArgs e)
		{
			// Update on UI thread
			Dispatcher.Invoke(() =>
			{
				// Update game status
				switch (e.State)
				{
					case GameState.WhiteWinsByCheckmate:
						GameStatusText.Text = "White wins by checkmate!";
						GameStatusText.Foreground = Brushes.LightGreen;
						break;
					case GameState.BlackWinsByCheckmate:
						GameStatusText.Text = "Black wins by checkmate!";
						GameStatusText.Foreground = Brushes.LightGreen;
						break;
					case GameState.DrawByStalemate:
						GameStatusText.Text = "Draw by stalemate";
						GameStatusText.Foreground = Brushes.Yellow;
						break;
					case GameState.DrawByInsufficientMaterial:
						GameStatusText.Text = "Draw by insufficient material";
						GameStatusText.Foreground = Brushes.Yellow;
						break;
					case GameState.DrawByFiftyMoveRule:
						GameStatusText.Text = "Draw by fifty-move rule";
						GameStatusText.Foreground = Brushes.Yellow;
						break;
					case GameState.DrawByRepetition:
						GameStatusText.Text = "Draw by threefold repetition";
						GameStatusText.Foreground = Brushes.Yellow;
						break;
					default:
						GameStatusText.Text = "Game in progress";
						GameStatusText.Foreground = Brushes.White;
						break;
				}

				// Update PGN
				PgnText.Text = game.PgnMoveHistory;
			});
		}

		private void ChessBoard_MoveCompleted(object sender, MoveEventArgs e)
		{
			// Add move to history
			string moveText = $"{game.MoveNumber - 1}. {GetAlgebraicNotation(e.Move)}";
			MoveHistoryListBox.Items.Add(moveText);
			MoveHistoryListBox.ScrollIntoView(MoveHistoryListBox.Items[MoveHistoryListBox.Items.Count - 1]);

			// Update PGN
			PgnText.Text = game.PgnMoveHistory;
		}

		private void ChessBoard_AIThinking(object sender, AIThinkingEventArgs e)
		{
			// Update on UI thread
			Dispatcher.Invoke(() =>
			{
				// Show AI thinking progress
				AIStatusText.Text = $"AI is thinking... {e.ProgressPercentage}%";
				AIStatusText.Visibility = Visibility.Visible;

				AIProgressBar.Value = e.ProgressPercentage;
				AIProgressBar.Visibility = Visibility.Visible;

				if (e.ProgressPercentage >= 100)
				{
					// Hide progress after a delay
					System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
					{
						Dispatcher.Invoke(() =>
						{
							AIStatusText.Visibility = Visibility.Collapsed;
							AIProgressBar.Visibility = Visibility.Collapsed;
						});
					});
				}
			});
		}

		private void UpdateGameInfo()
		{
			// Update current player
			if (game.CurrentPlayer.Color == PieceColor.White)
			{
				CurrentPlayerIndicator.Fill = Brushes.White;
				CurrentPlayerText.Text = "White to move";
			}
			else
			{
				CurrentPlayerIndicator.Fill = Brushes.Black;
				CurrentPlayerText.Text = "Black to move";
			}

			// Update move number
			MoveNumberText.Text = $"Move: {game.MoveNumber}";
		}

		private string GetAlgebraicNotation(Move move)
		{
			char fromFile = (char)('a' + move.FromX);
			char fromRank = (char)('1' + move.FromY);
			char toFile = (char)('a' + move.ToX);
			char toRank = (char)('1' + move.ToY);

			return $"{fromFile}{fromRank}-{toFile}{toRank}";
		}

		private void NewGameButton_Click(object sender, RoutedEventArgs e)
		{
			InitializeGame();
		}

		private void UndoMoveButton_Click(object sender, RoutedEventArgs e)
		{
			if (game.UndoMove())
			{
				// Remove last two moves from history (player and AI)
				if (MoveHistoryListBox.Items.Count >= 2)
				{
					MoveHistoryListBox.Items.RemoveAt(MoveHistoryListBox.Items.Count - 1);
					MoveHistoryListBox.Items.RemoveAt(MoveHistoryListBox.Items.Count - 1);
				}

				// Update PGN
				PgnText.Text = game.PgnMoveHistory;
			}
		}

		private void GetHintButton_Click(object sender, RoutedEventArgs e)
		{
			string hint = game.GetAIHint();
			MessageBox.Show($"Suggested move: {hint}", "Hint", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		private void AIDifficultySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			// Only reinitialize if the game is already in progress, the value has actually changed,
			// and we're not already in the process of initializing
			if (game != null && e.OldValue != e.NewValue && !isInitializing)
			{
				InitializeGame();
			}
		}
	}
}

