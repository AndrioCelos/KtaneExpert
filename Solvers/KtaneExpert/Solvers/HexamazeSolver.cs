using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using AngelAiml;
using JetBrains.Annotations;

namespace KtaneExpert.Solvers;
public class HexamazeSolver : IModuleSolver {
	private const int Size = 12;
	private const int SubmazeSize = 4;
	private const int Sw = 2 * Size + 1;

	private static readonly Cache<int, Maze> mazeCache = new(Utils.CacheSize, GetMazeInternal);

	public static Maze GetMaze(int ruleSeed) => mazeCache.Get(ruleSeed);

	private static Maze GetMazeInternal(int ruleSeed) {
		if (ruleSeed == 1) return DefaultMaze;

		var rnd = new MonoRandom(ruleSeed);

		// PART 1: GENERATE MAZE (walls)
		var walls = Enumerable.Range(0, 3 * Sw * Sw).Select(i => {
			var hex = SpaceIndexToHex(i / 3);
			return hex.Distance < Size || hex.GetNeighbor((Direction) (i % 3)).Distance < Size;
		}).ToArray();
		var allWalls = (bool[]) walls.Clone();

		var stack = new Stack<Hex>();
		var curHex = new Hex(0, 0);
		stack.Push(curHex);
		var taken = new HashSet<Hex> { curHex };

		// Step 1.1: generate a single giant maze
		while (true) {
			var potentialNeighbourDirections = Enumerable.Range(0, 6).Select(i => (Direction) i).Where(d => { var n = curHex.GetNeighbor(d); return !taken.Contains(n) && n.Distance < Size; }).ToArray();
			if (potentialNeighbourDirections.Length == 0) {
				if (stack.Count == 0)
					break;
				curHex = stack.Pop();
				continue;
			}
			var dir = potentialNeighbourDirections[rnd.Next(0, potentialNeighbourDirections.Length)];
			walls[WallIndex(curHex, dir)] = false;
			stack.Push(curHex);
			curHex = curHex.GetNeighbor(dir);
			taken.Add(curHex);
		}

		// Step 1.2: Go through all submazes and make sure they’re all connected and all have at least one exit on each side
		// This is parallelizable and uses multiple threads
		var allSubmazes = Hex.LargeHexagon(Size - SubmazeSize + 1).Select(h => (Hex?) h).ToArray();
		Hex? lastHex1 = null, lastHex2 = null;
		while (true) {
			var candidateCounts = new Dictionary<int, int>();

			for (var smIx = 0; smIx < allSubmazes.Length; smIx++) {
				var submaze = allSubmazes[smIx];
				if (submaze == null)
					continue;

				var centerHex = submaze.Value;

				// We do not need to examine this submaze if the wall we last removed isn’t even in it
				if (lastHex1 != null && (lastHex1.Value - centerHex).Distance > SubmazeSize &&
					lastHex2 != null && (lastHex2.Value - centerHex).Distance > SubmazeSize)
					continue;

				var validity = DetermineSubmazeValidity(centerHex, walls);
				if (validity.IsValid) {
					allSubmazes[smIx] = null;
					continue;
				}

				// Find out which walls might benefit from removing
				foreach (var fh in validity.Filled) {
					for (Direction dir = 0; dir < (Direction) 6; dir++) {
						var th = fh.GetNeighbor(dir);
						var offset = th - centerHex;
						if ((offset.Distance < SubmazeSize && walls[WallIndex(fh, dir)] && !validity.Filled.Contains(th)) ||
							(offset.Distance == SubmazeSize && offset.GetEdges(SubmazeSize).Any(e => !validity.EdgesReachable[(int) e]))) {
							var index = WallIndex(fh, dir);
							candidateCounts[index] = candidateCounts.TryGetValue(index, out var count) ? count + 1 : 1;
						}
					}
				}
			}

			if (candidateCounts.Count == 0)
				break;

			// Remove one wall out of the “most wanted”
			var topScore = 0;
			var topScorers = new List<int>();
			foreach (var kvp in candidateCounts)
				if (kvp.Value > topScore) {
					topScore = kvp.Value;
					topScorers.Clear();
					topScorers.Add(kvp.Key);
				} else if (kvp.Value == topScore)
					topScorers.Add(kvp.Key);
			topScorers.Sort();
			var randomWall = topScorers[rnd.Next(0, topScorers.Count)];
			walls[randomWall] = false;

			var rcdir = (Direction) (randomWall % 3);
			lastHex1 = new Hex(randomWall / 3 / Sw - Size, randomWall / 3 % Sw - Size);
			lastHex2 = lastHex1.Value.GetNeighbor(rcdir);
		}

		// Step 1.3: Put as many walls back in as possible
		var missingWalls = Enumerable.Range(0, allWalls.Length).Where(ix => allWalls[ix] && !walls[ix]).ToList();
		while (missingWalls.Count > 0) {
			var randomMissingWallIndex = rnd.Next(0, missingWalls.Count);
			var randomMissingWall = missingWalls[randomMissingWallIndex];
			missingWalls.RemoveAt(randomMissingWallIndex);
			walls[randomMissingWall] = true;

			var affectedHex1 = new Hex(randomMissingWall / 3 / Sw - Size, randomMissingWall / 3 % Sw - Size);
			var affectedHex2 = affectedHex1.GetNeighbor((Direction) (randomMissingWall % 3));

			var list = Hex.LargeHexagon(Size - SubmazeSize + 1).ToList();
			rnd.ShuffleFisherYates(list);
			foreach (var centerHex in list) {
				// We do not need to examine this submaze if the wall we put in isn’t even in it
				if (((affectedHex1 - centerHex).Distance <= SubmazeSize ||
					(affectedHex2 - centerHex).Distance <= SubmazeSize) &&
					!DetermineSubmazeValidity(centerHex, walls).IsValid) {
					// This wall cannot be added, take it back out.
					walls[randomMissingWall] = false;
					break;
				}
			}
		}

		// PART 2: GENERATE MARKINGS
	tryAgain:
		var markings = new Marking[Sw * Sw];
		// List Circle and Hexagon twice so that triangles don’t completely dominate the distribution
		var allowedMarkings = new[] { Marking.Circle, Marking.Circle, Marking.Hexagon, Marking.Hexagon, Marking.TriangleDown, Marking.TriangleLeft, Marking.TriangleRight, Marking.TriangleUp };

		// Step 2.1: Put random markings in until there are no more ambiguities
		while (!AreMarkingsUnique(markings)) {
			var availableHexes = Hex.LargeHexagon(Size)
				.Where(h => markings[MarkingIndex(h)] == Marking.None &&
					h.GetNeighbors().SelectMany(n1 => n1.GetNeighbors())
						.All(n2 => n2.Distance >= Size || markings[MarkingIndex(n2)] == Marking.None))
				.ToArray();
			if (availableHexes.Length == 0)
				goto tryAgain;
			var randomHex = availableHexes[rnd.Next(0, availableHexes.Length)];
			markings[MarkingIndex(randomHex)] = allowedMarkings[rnd.Next(0, allowedMarkings.Length)];
		}

		// Step 2.2: Find markings to remove again
		var removableMarkings = Enumerable.Range(0, markings.Length).Where(i => markings[i] != Marking.None).ToList();
		while (removableMarkings.Count > 0) {
			var tryRemove = Utils.RemoveRandom(removableMarkings, rnd);
			var prevMarking = markings[tryRemove];
			markings[tryRemove] = Marking.None;
			if (!AreMarkingsUnique(markings)) {
				// No longer unique — put it back in
				markings[tryRemove] = prevMarking;
			}
		}

		return new(walls, markings);
	}

	private static Maze DefaultMaze { get; } = new(
		[false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, false, false, true, false, false, true, false, false, true, false, false, false, false, false, true, false, false, true, false, false, true, false, false, true, false, false, true, false, false, false, false, false, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, false, true, true, false, true, true, true, true, false, false, true, true, false, true, true, false, true, false, true, false, false, true, true, false, false, true, false, true, true, false, true, true, true, false, false, true, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, true, true, true, false, true, true, false, true, true, false, false, true, true, false, true, true, false, true, false, true, true, false, false, true, true, false, true, true, false, true, true, true, true, true, true, true, true, false, false, true, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, true, false, true, true, true, true, true, true, true, true, false, true, false, true, true, false, true, true, true, true, true, true, true, true, true, true, true, true, false, true, true, true, false, true, true, false, false, true, true, true, true, false, true, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, false, true, true, true, true, false, true, true, false, true, true, true, false, true, true, false, true, false, true, true, false, false, true, false, false, true, false, true, false, true, false, false, false, false, true, false, false, true, true, true, false, false, false, true, false, false, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, true, false, false, true, true, false, true, true, false, true, true, false, true, true, true, true, false, false, true, true, false, true, true, true, true, false, false, true, false, true, true, false, true, true, false, true, false, false, false, true, false, true, true, false, false, true, true, false, true, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, true, false, false, true, true, false, true, true, false, true, false, false, false, true, false, true, false, false, true, false, false, false, true, false, false, true, false, false, false, true, true, false, true, true, false, true, true, false, true, true, false, true, false, true, true, false, true, false, true, true, false, false, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, true, false, true, false, true, false, true, true, false, false, false, true, true, false, false, false, false, true, true, false, true, false, true, true, false, true, true, false, false, true, true, false, true, false, false, true, true, false, false, true, true, true, false, true, true, false, true, true, false, false, false, true, false, false, true, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, false, true, true, false, false, true, true, true, true, true, true, true, false, true, true, false, true, true, false, false, true, true, true, false, true, true, false, true, true, false, false, true, true, false, false, true, false, false, true, false, false, false, true, true, false, false, true, true, false, true, false, true, true, false, true, false, true, false, false, false, false, false, false, false, false, false, false, false, false, false, true, true, false, true, true, false, false, true, false, false, false, true, false, false, false, true, true, true, true, false, true, false, true, true, false, true, true, false, true, true, true, true, false, true, true, false, true, true, false, true, true, false, true, false, true, true, false, false, true, true, false, true, false, true, true, true, false, true, false, true, false, true, true, false, false, false, false, false, false, false, false, false, true, true, true, true, false, false, true, true, false, true, true, true, true, false, true, true, false, true, false, false, true, false, true, true, false, true, true, false, true, true, false, true, true, true, true, false, true, true, false, true, true, false, false, true, false, true, true, false, true, true, false, true, false, true, true, false, true, true, true, false, false, true, true, false, true, true, false, false, false, false, false, false, true, true, false, true, false, false, true, true, false, true, true, false, false, false, false, false, true, true, true, false, true, false, false, true, false, false, true, false, false, false, false, false, true, false, false, true, true, false, true, false, true, true, true, true, false, false, true, true, false, true, true, true, false, true, false, true, false, true, false, true, true, false, true, true, false, true, false, true, true, false, false, false, false, true, true, true, true, true, true, false, true, true, true, true, true, true, false, false, true, true, false, true, true, true, false, true, true, false, true, true, false, false, false, true, false, true, false, true, true, false, false, false, false, true, false, false, true, true, false, true, false, false, true, false, true, true, true, false, true, true, false, true, true, false, false, true, false, true, false, true, true, false, true, false, false, false, false, true, true, true, true, false, true, false, false, false, false, true, false, true, true, false, false, false, false, false, true, true, true, false, true, true, false, true, true, true, false, true, false, true, true, false, true, false, true, true, true, false, true, true, false, true, true, false, true, false, false, false, false, false, true, true, false, true, true, true, false, true, true, false, true, true, false, true, true, false, false, false, false, false, false, false, true, true, true, true, false, true, false, true, true, true, false, false, true, true, false, false, true, true, false, false, true, false, true, true, false, true, true, true, false, true, false, false, false, false, true, false, false, true, true, false, true, false, true, false, false, true, false, true, false, true, false, true, false, true, true, false, true, false, true, true, true, false, true, true, true, false, false, false, false, false, false, false, false, false, false, false, true, false, true, true, true, true, false, false, true, true, false, false, true, false, true, false, false, false, false, false, false, true, true, false, true, true, true, false, true, false, true, true, false, true, true, false, true, true, true, false, true, true, false, true, true, false, false, false, true, true, true, false, true, true, false, true, false, true, true, false, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, true, true, true, true, true, false, false, true, true, true, true, true, true, true, true, true, false, false, false, false, false, false, false, false, true, false, false, false, false, true, true, false, true, true, false, false, true, false, false, true, true, true, false, true, false, true, false, false, true, false, true, true, true, false, true, true, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, true, true, true, false, true, true, false, false, false, true, false, true, true, true, false, false, true, true, false, true, false, false, true, false, false, false, true, false, false, false, true, false, true, false, false, false, true, true, true, false, false, true, false, false, true, true, false, true, true, false, true, false, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, true, false, true, false, true, true, false, false, false, true, true, false, true, true, false, true, true, true, false, true, true, false, true, true, false, false, true, true, false, true, true, false, true, false, true, true, false, true, true, false, true, false, true, true, false, true, true, true, false, true, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, false, true, true, true, true, true, true, true, false, false, true, true, false, false, true, false, false, true, true, false, true, true, false, false, true, true, false, true, true, true, false, true, true, false, true, true, false, true, true, false, true, true, false, true, false, true, true, true, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, true, true, false, false, true, false, true, true, true, true, true, true, true, false, true, true, false, true, false, true, false, true, false, false, false, false, false, true, false, true, false, false, false, false, true, false, true, true, false, true, true, true, false, false, true, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, true, true, false, false, true, false, true, true, true, true, false, false, true, true, false, true, false, true, true, false, true, true, false, false, true, true, true, true, false, true, false, true, true, false, true, false, false, false, true, true, true, true, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, true, false, true, true, false, true, false, false, true, false, true, true, true, true, true, false, true, true, false, false, true, false, true, true, false, false, true, false, true, false, false, true, true, false, true, false, false, false, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, false, true, true, true, true, true, false, true, false, true, false, true, true, true, true, true, true, true, true, true, false, true, true, true, true, true, true, true, true, true, true, true, true, true, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, true, false, false, true, false, false, true, false, false, true, false, false, true, false, false, true, false, false, true, false, false, true, false, false, false, false, false, true, false, false, true, false, false, true, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false],
		[Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.Hexagon, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.TriangleLeft, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.Circle, Marking.None, Marking.None, Marking.Circle, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.Hexagon, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.TriangleDown, Marking.None, Marking.None, Marking.None, Marking.TriangleUp, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.Circle, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.TriangleRight, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.TriangleRight, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.Hexagon, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.TriangleUp, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.Circle, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.Hexagon, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.Circle, Marking.None, Marking.None, Marking.None, Marking.TriangleDown, Marking.None, Marking.None, Marking.None, Marking.TriangleRight, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.TriangleLeft, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.Hexagon, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None, Marking.None]
	);

	private static (Hex hex, Direction wall) WallIndexToCoords(int index) => (SpaceIndexToHex(index / 3), (Direction) (index % 3));
	private static Hex SpaceIndexToHex(int index) => new(index / Sw - Size, index % Sw - Size);

	private static SubmazeValidity DetermineSubmazeValidity(Hex centerHex, bool[] walls) {
		var edgesReachable = new bool[6];
		var filled = new HashSet<Hex> { centerHex };
		var q = new Queue<Hex>();
		q.Enqueue(centerHex);

		// Flood-fill as much of the maze as possible
		while (q.Count > 0) {
			var hex = q.Dequeue();
			for (Direction dir = 0; dir < (Direction) 6; dir++) {
				var neighbour = hex.GetNeighbor(dir);
				var offset = neighbour - centerHex;
				if (offset.Distance < SubmazeSize && !HasWall(hex, dir, walls) && filled.Add(neighbour))
					q.Enqueue(neighbour);
				if (offset.Distance == SubmazeSize && !HasWall(hex, dir, walls))
					foreach (var edge in offset.GetEdges(SubmazeSize))
						edgesReachable[(int) edge] = true;
			}
		}

		var isValid =
			// All hexes filled?
			(filled.Count >= 3 * SubmazeSize * (SubmazeSize - 1) + 1) &&
			// All edges reachable?
			!edgesReachable.Contains(false);
		return new(isValid, filled, edgesReachable);
	}

	private static bool AreMarkingsUnique(Marking[] markings) {
		var unique = new HashSet<string>();
		foreach (var centerHex in Hex.LargeHexagon(Size - SubmazeSize + 1))
			for (var rotation = 0; rotation < 6; rotation++)
				if (!unique.Add(string.Join(null, Hex.LargeHexagon(SubmazeSize).Select(h => (int) RotateMarking(markings[MarkingIndex(h.Rotate(rotation) + centerHex)], -rotation)))))
					return false;
		return true;
	}

	private static int MarkingIndex(Hex h) => (h.Q + Size) * Sw + h.R + Size;

	private static int WallIndex(Hex hex, Direction dir) {
		while (dir >= Direction.Clock4) {
			hex = hex.GetNeighbor(dir);
			dir -= 3;
		}
		return 3 * Sw * (hex.Q + Size) + 3 * (hex.R + Size) + (int) dir;
	}

	private static bool HasWall(Hex hex, Direction dir, IReadOnlyList<bool> walls) => walls[WallIndex(hex, dir)];

	private static Marking RotateMarking(Marking marking, int rotation)
		=> marking is Marking.TriangleUp or Marking.TriangleDown or Marking.TriangleLeft or Marking.TriangleRight
			// ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
			? marking ^ (Marking) (rotation & 1)
			: marking;

	private static Direction RotateDirection(Direction direction, int rotation) => (Direction) ((((int) direction + rotation) % 6 + 6) % 6);

	private static bool IsOnSubmazeEdge(Hex submazeHex, int submazeRotation, Direction direction, GridEdge edge) {
		var rotatedHex = submazeHex.Rotate(submazeRotation);
		var rotatedDirection = (Direction) ((int) direction + submazeRotation % 6);
		return edge switch {
			GridEdge.Clock11 => rotatedDirection is Direction.Clock10 or Direction.Clock12 && rotatedHex.Q + rotatedHex.R == -(SubmazeSize - 1),
			GridEdge.Clock1 => rotatedDirection is Direction.Clock12 or Direction.Clock2 && rotatedHex.R == -(SubmazeSize - 1),
			GridEdge.Clock3 => rotatedDirection is Direction.Clock2 or Direction.Clock4 && rotatedHex.Q == SubmazeSize - 1,
			GridEdge.Clock5 => rotatedDirection is Direction.Clock4 or Direction.Clock6 && rotatedHex.Q + rotatedHex.R == SubmazeSize - 1,
			GridEdge.Clock7 => rotatedDirection is Direction.Clock6 or Direction.Clock8 && rotatedHex.R == SubmazeSize - 1,
			GridEdge.Clock9 => rotatedDirection is Direction.Clock8 or Direction.Clock10 && rotatedHex.Q == -(SubmazeSize - 1),
			_ => throw new ArgumentException("Invalid edge", nameof(edge))
		};
	}

	public void GenerateAiml(string path, int ruleSeed) => throw new NotImplementedException();
	public string Process(string text, XElement element, RequestProcess process) {
		// Parameters: [rule seed] [colour]|[marking] [x] [y] ...
		var words = text.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
		var maze = GetMaze(int.Parse(words[0]));

		Colour? pawnColour = null; Hex pawnPositionInSubmaze = default;
		var markings = new Dictionary<Hex, Marking>();

		// Parse arguments.
		for (var i = 1; i < words.Length; i += 3) {
			var x = int.Parse(words[i + 1]);
			var y = int.Parse(words[i + 2]);
			if (Enum.TryParse<Colour>(words[i], true, out var colour)) {
				if (pawnColour != null) throw new FormatException();
				pawnColour = colour;
				pawnPositionInSubmaze = Hex.FromColumnCoordinates(x, y, 4);
			} else if (Enum.TryParse<Marking>(words[i], true, out var marking)) {
				var hex = Hex.FromColumnCoordinates(x, y, 4);
				markings.Add(hex, marking);
			}
		}

		if (pawnColour == null) throw new ArgumentException("No pawn specified");

		// Find the submaze.
		foreach (var centre in Hex.LargeHexagon(Size - SubmazeSize + 1)) {
			for (var rotation = 0; rotation < 6; rotation++) {
				var markingCount = 0; var valid = true;
				foreach (var hex in Hex.LargeHexagon(SubmazeSize)) {
					var marking = RotateMarking(maze.GetMarking(hex.Rotate(rotation) + centre), rotation);
					if (marking != 0) {
						if (!(markings.TryGetValue(hex, out var actualMarking) && actualMarking == marking)) {
							valid = false;
							break;
						}
						markingCount++;
					}
				}
				if (valid && markingCount == markings.Count) {
					Console.WriteLine($"Submaze found: {centre} @ {rotation}");
					// Submaze found. Now find a path out.
					var path = new Stack<Direction>();
					var openSet = new Queue<(Hex hex, Direction? dir)>();
					openSet.Enqueue((pawnPositionInSubmaze, null));
					var closedSet = new Dictionary<Hex, Direction?>();

					// Perform a breadth-first search to find a path.
					while (openSet.Count > 0) {
						var (ph, dir) = openSet.Dequeue();
						if (closedSet.ContainsKey(ph)) continue;

						var reverseDir = dir != null ? (Direction?) (((int) dir + 3) % 6) : null;
						closedSet.Add(ph, dir);

						for (Direction dir2 = 0; dir2 < (Direction) 6; dir2++) {
							if (dir2 == reverseDir) continue;
							if (HasWall(ph.Rotate(rotation) + centre, RotateDirection(dir2, rotation), maze.Walls)) continue;
							if (IsOnSubmazeEdge(ph, rotation, dir2, (GridEdge) pawnColour)) {
								// Goal found! Now retrace the path.
								path.Push(dir2);
								while (true) {
									dir = closedSet[ph];
									if (dir == null) return string.Join(' ', path.Select(d => d switch { Direction.Clock2 => "2", Direction.Clock4 => "4", Direction.Clock6 => "6", Direction.Clock8 => "8", Direction.Clock10 => "10", Direction.Clock12 => "12", _ => "unknown" }));
									path.Push(dir.Value);
									// The value is which direction we moved to get here, so reverse it.
									ph = ph.GetNeighbor((Direction) (((int) dir + 3) % 6));
								}
							}
							var neighbour = ph.GetNeighbor(dir2);
							if (!closedSet.ContainsKey(neighbour) && neighbour.Distance < SubmazeSize)
								openSet.Enqueue((neighbour, dir2));
						}
					}

					return "NoSolution";

				}
			}
		}

		return "NoSubmaze";
	}

	public enum Direction {
		Clock10,
		Clock12,
		Clock2,
		Clock4,
		Clock6,
		Clock8
	}

	public enum GridEdge {
		Clock11,
		Clock1,
		Clock3,
		Clock5,
		Clock7,
		Clock9
	}

	[UsedImplicitly(ImplicitUseTargetFlags.Members)]
	public enum Colour {
		Red,
		Yellow,
		Green,
		Cyan,
		Blue,
		Pink
	}

	/// <summary>Represents coordinates of a space on a hexagonal grid.</summary>
	public readonly struct Hex(int q, int r) : IEquatable<Hex> {
		/// <summary>Returns the number of columns right of the centre.</summary>
		/// <remarks>The positive <see cref="Q"/> direction is at 4 o-clock.</remarks>
		public int Q { get; } = q;
		/// <summary>Returns the number of spaces below the space in the column specified by <see cref="Q"/> along the 4-/10-o-clock diagonal from the centre.</summary>
		/// <remarks>The positive <see cref="R"/> direction is at 6 o-clock.</remarks>
		public int R { get; } = r;

		public static IEnumerable<Hex> LargeHexagon(int sideLength) {
			for (var r = -sideLength + 1; r < sideLength; r++)
				for (var q = -sideLength + 1; q < sideLength; q++) {
					var hex = new Hex(q, r);
					if (hex.Distance < sideLength)
						yield return hex;
				}
		}

		public IEnumerable<Hex> GetNeighbors() {
			yield return new(Q - 1, R);
			yield return new(Q    , R - 1);
			yield return new(Q + 1, R - 1);
			yield return new(Q + 1, R);
			yield return new(Q    , R + 1);
			yield return new(Q - 1, R + 1);
		}

		public Hex GetNeighbor(Direction dir) => dir switch {
			Direction.Clock10 => new(Q - 1, R),
			Direction.Clock12 => new(Q    , R - 1),
			Direction.Clock2  => new(Q + 1, R - 1),
			Direction.Clock4  => new(Q + 1, R),
			Direction.Clock6  => new(Q    , R + 1),
			Direction.Clock8  => new(Q - 1, R + 1),
			_ => throw new ArgumentOutOfRangeException(nameof(dir)),
		};

		public int Distance => Math.Max(Math.Abs(Q), Math.Max(Math.Abs(R), Math.Abs(-Q - R)));

		/// <summary>Returns a sequence of edges of this space that are on edges of the containing grid of the specified size.</summary>
		/// <remarks>Coordinates of this space should be relative to the centre of the grid.</remarks>
		public IEnumerable<GridEdge> GetEdges(int size) {
			// Don’t use ‘else’ because multiple conditions could apply
			if (Q + R == -size)
				yield return GridEdge.Clock11;
			if (R == -size)
				yield return GridEdge.Clock1;
			if (Q == size)
				yield return GridEdge.Clock3;
			if (Q + R == size)
				yield return GridEdge.Clock5;
			if (R == size)
				yield return GridEdge.Clock7;
			if (Q == -size)
				yield return GridEdge.Clock9;
		}

		public override string ToString() => $"({Q}, {R})";

		public bool Equals(Hex other) => Q == other.Q && R == other.R;
		public override bool Equals(object? obj) => obj is Hex hex && Equals(hex);
		public static bool operator ==(Hex one, Hex two) => one.Q == two.Q && one.R == two.R;
		public static bool operator !=(Hex one, Hex two) => one.Q != two.Q || one.R != two.R;
		public override int GetHashCode() => Q * 47 + R;

		public static Hex operator +(Hex one, Hex two) => new(one.Q + two.Q, one.R + two.R);
		public static Hex operator -(Hex one, Hex two) => new(one.Q - two.Q, one.R - two.R);

		/// <summary>Returns a <see cref="Hex"/> representing the current instance rotated the specified number of 60° steps clockwise around the origin.</summary>
		public Hex Rotate(int rotation) => ((rotation % 6 + 6) % 6) switch {
			0 => this,
			1 => new(-R, Q + R),
			2 => new(-Q - R, Q),
			3 => new(-Q, -R),
			4 => new(R, -Q - R),
			5 => new(Q + R, -Q),
			_ => throw new InvalidOperationException(),
		};

		public static Hex FromColumnCoordinates(int x, int y, int size) => new(x - size, x < size ? y - x : y - size);

		/// <summary>Returns a string representation of column-based coordinates for this instance.</summary>
		/// <remarks>
		/// Column-based coordinates are a common method of communicating <see cref="Hex"/> coordinates.
		/// The first number represents the number of columns right of the left-most column. The second number represents the number of spaces below the topmost space of that column.
		/// </remarks>
		public string ConvertCoordinates(int sideLength) => Q >= 0
			? $"({Q + sideLength}, {R + sideLength})"
			: $"({Q + sideLength}, {Q + R + sideLength})";
	}

	public enum Marking {
		None,
		Circle,
		TriangleUp,
		TriangleDown,
		TriangleLeft,
		TriangleRight,
		Hexagon
	}

	public class Maze(bool[] walls, Marking[] markings) {
		private const int StringWidth = 6 * Size - 2;
		private const int StringHeight = 4 * Size - 1;
		private readonly bool[] walls = walls;
		private readonly Dictionary<Hex, Marking> markings = Enumerable.Range(0, markings.Length).Where(i => markings[i] != Marking.None).ToDictionary(SpaceIndexToHex, i => markings[i]);

		public IReadOnlyList<bool> Walls { get; } = Array.AsReadOnly(walls);

		public override string ToString() {
			var lines = Enumerable.Range(0, StringHeight).Select(_ => new StringBuilder(new string(' ', StringWidth))).ToArray();

			// Draw walls.
			for (var ix = 0; ix < walls.Length; ix++) {
				if (!walls[ix]) continue;

				var (hex, dir) = WallIndexToCoords(ix);
				switch (dir) {
					// NW wall
					case Direction.Clock10:
						lines[StringHeight / 2 + hex.Q + 2 * hex.R][StringWidth / 2 + 3 * hex.Q - 2] = '/';
						break;

					// N wall
					case Direction.Clock12: {
						lines[StringHeight / 2 + hex.Q + 2 * hex.R - 1][StringWidth / 2 + 3 * hex.Q - 1] = '_';
						lines[StringHeight / 2 + hex.Q + 2 * hex.R - 1][StringWidth / 2 + 3 * hex.Q] = '_';
					}
					break;

					// NE wall
					case Direction.Clock2:
						lines[StringHeight / 2 + hex.Q + 2 * hex.R][StringWidth / 2 + 3 * hex.Q + 1] = '\\';
						break;
				}
			}

			// Draw markings.
			foreach (var (hex, marking) in markings) {
				lines[StringHeight / 2 + hex.Q + 2 * hex.R][StringWidth / 2 + 3 * hex.Q - 1] = marking switch {
					Marking.Circle => '(',
					Marking.TriangleUp => '/',
					Marking.TriangleDown => '\\',
					Marking.TriangleLeft => '<',
					Marking.TriangleRight => '|',
					Marking.Hexagon => '{',
					_ => throw new NotImplementedException()
				};
				lines[StringHeight / 2 + hex.Q + 2 * hex.R][StringWidth / 2 + 3 * hex.Q] = marking switch {
					Marking.Circle => ')',
					Marking.TriangleUp => '\\',
					Marking.TriangleDown => '/',
					Marking.TriangleLeft => '|',
					Marking.TriangleRight => '>',
					Marking.Hexagon => '}',
					_ => throw new NotImplementedException()
				};
			}

			return string.Join('\n', lines.Select(b => b.ToString()));
		}

		public Marking GetMarking(Hex hex) => markings.GetValueOrDefault(hex, Marking.None);
	}

	private record SubmazeValidity(bool IsValid, HashSet<Hex> Filled, bool[] EdgesReachable);
}
