// Models/Position.cs
using System;

namespace ChessGame.Models
{
	/// <summary>
	/// Position class for tracking piece positions
	/// </summary>
	public class Position
	{
		public int X { get; }
		public int Y { get; }

		public Position(int x, int y)
		{
			X = x;
			Y = y;
		}

		public override bool Equals(object obj)
		{
			if (obj is Position other)
				return X == other.X && Y == other.Y;

			return false;
		}

		public override int GetHashCode()
		{
			return X * 8 + Y;
		}

		public override string ToString()
		{
			return $"({X}, {Y})";
		}
	}
}

