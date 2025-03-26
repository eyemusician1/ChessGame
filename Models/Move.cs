// Models/Move.cs
using System;
using System.Numerics;

namespace ChessGame.Models
{
	/// <summary>
	/// Represents a chess move
	/// </summary>
	public class Move
	{
		public int FromX { get; }
		public int FromY { get; }
		public int ToX { get; }
		public int ToY { get; }
		public Player Player { get; }

		public Move(int fromX, int fromY, int toX, int toY, Player player)
		{
			FromX = fromX;
			FromY = fromY;
			ToX = toX;
			ToY = toY;
			Player = player;
		}

		public static Move FromAlgebraic(string algebraic, Player player)
		{
			// Convert algebraic notation (e.g., "e2e4") to a Move object
			if (algebraic.Length != 4)
				return null;

			int fromX = algebraic[0] - 'a';
			int fromY = algebraic[1] - '1';
			int toX = algebraic[2] - 'a';
			int toY = algebraic[3] - '1';

			if (fromX < 0 || fromX > 7 || fromY < 0 || fromY > 7 ||
				toX < 0 || toX > 7 || toY < 0 || toY > 7)
				return null;

			return new Move(fromX, fromY, toX, toY, player);
		}

		public override string ToString()
		{
			char fromFile = (char)('a' + FromX);
			char fromRank = (char)('1' + FromY);
			char toFile = (char)('a' + ToX);
			char toRank = (char)('1' + ToY);

			return $"{fromFile}{fromRank}{toFile}{toRank}";
		}
	}
}

