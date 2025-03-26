// Models/HumanPlayer.cs
using System;
using System.Numerics;

namespace ChessGame.Models
{
	/// <summary>
	/// Human player implementation
	/// </summary>
	public class HumanPlayer : Player
	{
		public HumanPlayer(PieceColor color) : base(color) { }

		public override Move GetMove(Board board)
		{
			// This is just a placeholder for the console version
			// In the UI version, moves will be provided by the UI
			Console.WriteLine("Enter your move (e.g., e2e4):");
			string input = Console.ReadLine();

			return Move.FromAlgebraic(input, this);
		}
	}
}

