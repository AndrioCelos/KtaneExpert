using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AngelAiml;

namespace KtaneExpert.Solvers;
public class KnobSolver : IModuleSolver {
	public static Rule[] GetRules(int ruleSeed) {
		var random = new MonoRandom(ruleSeed);

		var unusedRows = new List<int>(Enumerable.Range(0, 64));
		var potentials = new List<int>();
		do {
			var row1 = Utils.RemoveRandom(unusedRows, random);
			var row2 = Utils.RemoveRandom(unusedRows, random);
			var row3 = Utils.RemoveRandom(unusedRows, random);

			var pattern1 = row1 << 6 | row2;
			var pattern2 = row1 << 6 | row3;

			var row4 = Utils.RemoveRandom(unusedRows, random);
			var row5 = Utils.RemoveRandom(unusedRows, random);
			var row6 = Utils.RemoveRandom(unusedRows, random);

			var pattern3 = row5 << 6 | row4;
			var pattern4 = row6 << 6 | row4;

			potentials.Add(pattern1);
			potentials.Add(pattern2);
			potentials.Add(pattern3);
			potentials.Add(pattern4);
		}
		while (potentials.Count < 8);

		var rules = new Rule[8];
		for (var i = 0; i < 8; i++)
			rules[i] = new((Position) (i / 2), Utils.RemoveRandom(potentials, random));

		return rules;
	}

	private static readonly List<(string name, int[] indices)> patterns = [
		("Top", new[] { 0, 1, 2, 3, 4, 5 } ),
		("Bottom", new[] { 6, 7, 8, 9, 10, 11 } ),
		("Left", new[] { 0, 1, 2, 6, 7, 8 } ),
		("Right", new[] { 3, 4, 5, 9, 10, 11 } ),
		("Outer", new[] { 0, 5, 6, 11 } ),
		("Middle", new[] { 1, 4, 7, 10 } ),
		("Inner", new[] { 2, 3, 8, 9 } ),
		("All", new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } )
	];

	private static readonly string[][] countPatterns = [["Left", "Right"], ["Top", "Bottom"]];

	public string Process(string text, XElement element, RequestProcess process) {
		// Usage: <rule seed> GetPattern | <rule seed> Lights (<light number>:<state>)*
		
		var fields = text.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
		var rules = GetRules(int.Parse(fields[0]));

		if (fields[1].Equals("GetPattern", StringComparison.InvariantCultureIgnoreCase)) {
			// Find out whether we can use counts.
			var list = GetCountPattern(rules);
			if (list is not null) return $"Counts {string.Join(' ', list)}";

			// Find the first pattern that uniquely determines the required position.
			var (name, indices) = GetPattern(rules);
			return name + " " + string.Join(" ", indices);
		}

		if (fields[1].Equals("Counts", StringComparison.InvariantCultureIgnoreCase)) {
			var counts = new List<(int[] indices, int count)>();
			for (var i = 2; i < fields.Length; ++i) {
				var tokens2 = fields[i].Split(':');
				var indices = patterns.First(e => e.name.Equals(tokens2[0], StringComparison.InvariantCultureIgnoreCase)).indices;
				counts.Add((indices, int.Parse(tokens2[1])));
			}

			Position? position2 = null;

			foreach (var rule in rules) {
				if (counts.All(e => e.indices.Count(i => rule.Lights[i]) == e.count)) {
					if (position2 != null && position2 != rule.Position)
						return "MultiplePositions";
					position2 = rule.Position;
				}
			}

			return position2?.ToString().ToLower() ?? "unknown";
		} else {
			var known = new List<(int index, bool on)>();

			for (var i = 2; i < fields.Length; ++i) {
				var fields2 = fields[i].Split([':']);
				known.Add((int.Parse(fields2[0]), fields2[1].Equals("on", StringComparison.CurrentCultureIgnoreCase)));
			}

			Position? position2 = null;

			foreach (var rule in rules) {
				if (known.Any(e => rule.Lights[e.index] != e.on)) continue;
				if (position2 != null && position2 != rule.Position)
					return "MultiplePositions";
				position2 = rule.Position;
			}

			return position2?.ToString().ToLower() ?? "unknown";
		}
	}

	private static string[]? GetCountPattern(Rule[] rules) {
		foreach (var list in countPatterns) {
			var valid = true;
			var mapping = new Dictionary<string, Position>();
			foreach (var rule in rules) {
				var key = string.Join(" ", list.Select(s => patterns.First(e => e.name.Equals(s, StringComparison.InvariantCultureIgnoreCase)).indices.Count(i => rule.Lights[i])));
				if (mapping.TryGetValue(key, out var position)) {
					if (position == rule.Position) continue;
					valid = false;
					break;
				}

				mapping[key] = rule.Position;
			}
			if (valid) return list;
		}
		return null;
	}
	private static (string name, int[] indices) GetPattern(Rule[] rules) {
		foreach (var (name, indices) in patterns) {
			var valid = true;
			var mapping = new Dictionary<string, Position>();
			foreach (var rule in rules) {
				var key = string.Join("", indices.Select(i => rule.Lights[i] ? '1' : '0'));
				if (mapping.TryGetValue(key, out var position)) {
					if (position != rule.Position) {
						valid = false;
						break;
					}
				} else
					mapping[key] = rule.Position;
			}
			if (valid) return (name, indices);
		}
		throw new InvalidOperationException("No valid pattern found for this rule seed?!");
	}

	public void GenerateAiml(string path, int ruleSeed) {
		var rules = GetRules(ruleSeed);

		using var writer = new StreamWriter(Path.Combine(path, "aiml", $"knob{ruleSeed}.aiml"));
		writer.WriteLine("<?xml version='1.0' encoding='UTF-8'?>");
		writer.WriteLine("<aiml version='2.0'>");

		writer.WriteLine($"<category><pattern>SolverFallback Knob {ruleSeed} GetPattern</pattern>");

		var countPattern = GetCountPattern(rules);
		if (countPattern is not null) {
			writer.WriteLine($"<template>Counts {string.Join(' ', countPattern)}</template>");
			writer.WriteLine("</category>");

			var used = new HashSet<string>();
			for (var i = 0; i < 8; ++i) {
				var rule = rules[i];
				var counts = string.Join(" ", countPattern.Select(s => $"{s}:{patterns.First(e => e.name.Equals(s, StringComparison.InvariantCultureIgnoreCase)).indices.Count(i => rule.Lights[i])}"));

				if (used.Add(counts)) {
					writer.WriteLine($"<category><pattern>SolverFallback Knob {ruleSeed} Counts {counts}</pattern>");
					writer.WriteLine($"<template>{rule.Position}</template>");
					writer.WriteLine("</category>");
				}
			}
		} else {
			var (name, indices) = GetPattern(rules);
			writer.WriteLine($"<template>{name} {string.Join(" ", indices)}</template>");
			writer.WriteLine("</category>");

			var used = new HashSet<string>();
			for (var i = 0; i < 8; ++i) {
				var rule = rules[i];
				var lights = string.Join(" ", indices.Select(j => j + ":" + (rule.Lights[j] ? "on" : "off")));

				if (used.Add(lights)) {
					writer.WriteLine($"<category><pattern>SolverFallback Knob {ruleSeed} Lights {lights}</pattern>");
					writer.WriteLine($"<template>{rule.Position}</template>");
					writer.WriteLine("</category>");
				}
			}
		}

		writer.WriteLine($"<category><pattern>SolverFallback Knob {ruleSeed} Lights *</pattern>");
		writer.WriteLine("<template>unknown</template>");
		writer.WriteLine("</category>");
		writer.WriteLine("</aiml>");
	}

	public enum Position {
		Up,
		Down,
		Left,
		Right
	}

	public record Rule(Position Position, params bool[] Lights) {
		public Rule(Position position, int lights) : this(position, ConvertLightsInt(lights)) { }

		private static bool[] ConvertLightsInt(int lights) {
			var array = new bool[12];
			for (var i = 0; i < 12; ++i) {
				array[i] = (lights & 1) != 0;
				lights >>= 1;
			}

			return array;
		}
	}
}
