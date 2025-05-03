using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AngelAiml;
using static KtaneExpert.Solvers.MazeSolver.MazeFlags;

namespace KtaneExpert.Solvers;
public class MazeSolver : IModuleSolver {
	private const int MazeCount = 9;
	private const int MazeSize = 6;

	private static readonly Cache<int, Maze[]> mazeCache = new(Utils.CacheSize, GetMazesInternal);

	public static Maze[] GetMazes(int ruleSeed) => mazeCache.Get(ruleSeed);

	private static Maze[] GetMazesInternal(int ruleSeed) {
		var random = new MonoRandom(ruleSeed);
		var mazes = new Maze[MazeCount];
		var indicatorPoss = new List<Point>();
		for (var x = 0; x < MazeSize; ++x) {
			for (var y = 0; y < MazeSize; ++y) {
				indicatorPoss.Add(new(x, y));
			}
		}

		for (var i = 0; i < MazeCount; ++i) {
			var maze = new MazeFlags[MazeSize, MazeSize];
			var visited = new HashSet<(int x, int y)>();

			for (var j = 0; j < MazeSize * (MazeSize - 1) * 2; ++j)
				random.NextDouble();

			var stack = new Stack<(int x, int y)>();
			var (x, y) = (random.Next(MazeSize), random.Next(MazeSize));
			while (true) {
				visited.Add((x, y));

				var point = GetNeighbour(x, y, visited, random);
				if (point.HasValue) {
					var (x2, y2) = point.Value;
					if (x2 - x > 0) {
						maze[x, y] |= East;
						maze[x2, y2] |= West;
					} else if (x2 - x < 0) {
						maze[x, y] |= West;
						maze[x2, y2] |= East;
					} else if (y2 - y > 0) {
						maze[x, y] |= South;
						maze[x2, y2] |= North;
					} else if (y2 - y < 0) {
						maze[x, y] |= North;
						maze[x2, y2] |= South;
					}
					stack.Push((x, y));
					(x, y) = (x2, y2);
				} else if (stack.Count > 0)
					(x, y) = stack.Pop();
				else
					break;
			}

			var indicators = new[] {
				Utils.RemoveRandom(indicatorPoss, random),
				Utils.RemoveRandom(indicatorPoss, random)
			};

			mazes[i] = new(indicators, maze);
		}

		return mazes;
	}

	private static (int x, int y)? GetNeighbour(int x, int y, HashSet<(int x, int y)> visited, Random random) {
		var list = new List<(int x, int y)>();
		if (x > 0 && !visited.Contains((x - 1, y))) list.Add((x - 1, y));
		if (x < MazeSize - 1 && !visited.Contains((x + 1, y))) list.Add((x + 1, y));
		if (y > 0 && !visited.Contains((x, y - 1))) list.Add((x, y - 1));
		if (y < MazeSize - 1 && !visited.Contains((x, y + 1))) list.Add((x, y + 1));

		return list.Count > 0 ? list[random.Next(list.Count)] : null;
	}

	private static bool TryGetMazeFromMarker(Maze[] mazes, int markerX, int markerY, [MaybeNullWhen(false)] out Maze maze) {
		maze = mazes.FirstOrDefault(m => m.Indicators.Contains(new(markerX, markerY)));
		return maze != null;
	}

	public string Process(string text, XElement element, RequestProcess process) {
		var inputWords = text.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
		var mazes = GetMazes(int.Parse(inputWords[0]));

		// The inputs are all one-based. Convert them to zero-based indices.
		var circleX = int.Parse(inputWords[1]) - 1;
		var circleY = int.Parse(inputWords[2]) - 1;
		var startX = int.Parse(inputWords[3]) - 1;
		var startY = int.Parse(inputWords[4]) - 1;
		var goalX = int.Parse(inputWords[5]) - 1;
		var goalY = int.Parse(inputWords[6]) - 1;

		// Find which maze this is.
		if (!TryGetMazeFromMarker(mazes, circleX, circleY, out var maze))
			return "NoMaze";

		// Solve the maze.
		var path = Search(maze, startX, startY, goalX, goalY);
		return path != null ? string.Join(" ", path) : "NoPath";
	}

	private static Stack<MazeFlags>? Search(Maze maze, int x, int y, int goalX, int goalY) {
		var path = new Stack<MazeFlags>();
		var openSet = new Queue<(int x, int y, MazeFlags dir)>();
		openSet.Enqueue((x, y, 0));
		var closedSet = new Dictionary<(int x, int y), MazeFlags>();

		// Perform a breadth-first search to find a path.
		while (openSet.Count > 0) {
			var (px, py, dir) = openSet.Dequeue();
			var reverseDir = dir switch {
				North => South,
				South => North,
				West => East,
				East => West,
				_ => (MazeFlags) 0,
			};
			closedSet.Add((px, py), dir);

			if (px == goalX && py == goalY) {
				// Goal found! Now retrace the path.
				while (true) {
					dir = closedSet[(px, py)];
					if (dir == 0) return path;
					path.Push(dir);
					// The value is which direction we moved to get here, so reverse it.
					switch (dir) {
						case North: ++py; break;
						case South: --py; break;
						case West: ++px; break;
						case East: --px; break;
					}
				}
			}

			var validDirections = maze.Cells[px, py] & ~reverseDir;
			if (validDirections.HasFlag(North)) openSet.Enqueue((px, py - 1, North));
			if (validDirections.HasFlag(South)) openSet.Enqueue((px, py + 1, South));
			if (validDirections.HasFlag(West )) openSet.Enqueue((px - 1, py, West ));
			if (validDirections.HasFlag(East )) openSet.Enqueue((px + 1, py, East ));
		}

		return null;
	}

	public void GenerateAiml(string path, int ruleSeed) {
		var mazes = GetMazes(ruleSeed);

		using (var writer = new StreamWriter(Path.Combine(path, "maps", $"MazeData{ruleSeed}.txt"))) {
			for (var mazeIndex = 0; mazeIndex < MazeCount; ++mazeIndex) {
				for (var x = 0; x < MazeSize; ++x) {
					for (var y = 0; y < MazeSize; ++y) {
						// In this library, the flags indicate valid directions.
						// In the AIML map, the flags indicate directions with walls.
						// We must invert the flags here.
						var flags = mazes[mazeIndex].Cells[x, y] ^ (MazeFlags) 0xF;
						writer.Write($"{mazeIndex + 1} {x + 1} {y + 1}:");
						if (flags == 0) writer.WriteLine("nil");
						else {
							if (flags.HasFlag(North)) writer.Write(nameof(North) + " ");
							if (flags.HasFlag(West)) writer.Write(nameof(West) + " ");
							if (flags.HasFlag(East)) writer.Write(nameof(East) + " ");
							if (flags.HasFlag(South)) writer.Write(nameof(South) + " ");
							writer.Flush();
							writer.BaseStream.Seek(-1, SeekOrigin.Current);  // Overwrite the last space.
							writer.WriteLine();
						}
					}
				}
			}
		}

		using (var writer = new StreamWriter(Path.Combine(path, "maps", $"MazeMarkers{ruleSeed}.txt"))) {
			for (var mazeIndex = 0; mazeIndex < MazeCount; ++mazeIndex) {
				foreach (var point in mazes[mazeIndex].Indicators) {
					writer.WriteLine($"{point.X + 1} {point.Y + 1}:{mazeIndex + 1}");
				}
			}
		}
	}

	[Flags]
	public enum MazeFlags {
		North = 1,
		West = 2,
		East = 4,
		South = 8
	}

	public readonly record struct Point(int X, int Y) {
		public override string ToString() => $"({X}, {Y})";
	}

	public record Maze(Point[] Indicators, MazeFlags[,] Cells);
}
