// Models/BoardUpdatedEventArgs.cs
using System;

namespace ChessGame.Models
{
	/// <summary>
	/// Event args for board updates
	/// </summary>
	public class BoardUpdatedEventArgs : EventArgs
	{
		public Board Board { get; }
		public string Message { get; }

		public BoardUpdatedEventArgs(Board board, string message = null)
		{
			Board = board;
			Message = message;
		}
	}
}

