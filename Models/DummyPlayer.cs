// Models/DummyPlayer.cs
using System;
using System.Numerics;

namespace ChessGame.Models
{
	/// <summary>
	/// Dummy player for move validation
	/// </summary>
	public class DummyPlayer : Player
	{
		public DummyPlayer(PieceColor color) : base(color) { }

		public override Move GetMove(Board board)
		{
			return null;
		}
	}
}

