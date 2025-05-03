using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AngelAiml;
using static KtaneExpert.Solvers.KeypadSolver.Glyph;

namespace KtaneExpert.Solvers;
public class KeypadSolver : IModuleSolver {
	public static Glyph[][] GetColumns(int ruleSeed) {
		var random = new MonoRandom(ruleSeed);
		var unusedGlyphs = new List<Glyph> {
			Copyright, FilledStar, HollowStar, SmileyFace, DoubleK, Omega,
			SquidKnife, Pumpkin, HookN, Teepee, Six, SquigglyN,
			AT, Ae, MeltedThree, Euro, Circle, NWithHat,
			Dragon, QuestionMark, Paragraph, RightC, LeftC, Pitchfork,
			Tripod, Cursive, Tracks, Balloon, WeirdNose, UpsideDownY,
			BT
		};
		var columns = new Glyph[6][];

		var singleGlyphs = new List<Glyph>();
		for (var i = 0; i < 6; i++) {
			var column = new Glyph[7];
			columns[i] = column;

			var j = 0;
			// Repeat up to 3 glyphs from previous columns.
			// (No glyph will appear in more than two columns.)
			if (i > 0) {
				while (j < 3) {
					if (singleGlyphs.Count == 0) break;
					var glyph = Utils.RemoveRandom(singleGlyphs, random);
					column[j] = glyph;
					j++;
				}
			}
			// Add unused glyphs. (These may be repeated later.)
			while (j < 7) {
				var glyph = Utils.RemoveRandom(unusedGlyphs, random);
				column[j] = glyph;
				singleGlyphs.Add(glyph);
				j++;
			}

			// Shuffle the column.
			for (j = 0; j < column.Length; j++) {
				var hold = column[j];
				var index = random.Next(column.Length);
				column[j] = column[index];
				column[index] = hold;
			}
		}

		return columns;
	}

	/// <remarks>
	/// Possible texts:
	/// <c>[rule seed] GetColumns</c>
	///     Returns the six columns, each terminated with 'end'
	/// <c>[rule seed] CheckKeypad [glyphs ...]</c>
	///     Returns true if any column has all of the specified glyphs; false otherwise.
	/// <c>[rule seed] Keypad [4 glyphs]</c> - specify the four glyphs on the keypad
	///     Returns the 4 glyphs in the order to press them.
	/// <c>[rule seed] Start</c> - start asking about glyphs
	/// <c>[rule seed] GlyphPresent [True|False] [glyph]</c> - indicate whether the specified glyph is present
	///     Both return one of the following:
	///         <c>Ask [glyph]</c> - the caller should ask the Defuser whether this glyph is on the keypad.
	///         <c>Tell [7 glyphs]</c> - the caller should present this column to the Defuser.
	/// </remarks>
	public string Process(string text, XElement element, RequestProcess process) {
		var words = text.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
		var columns = GetColumns(int.Parse(words[0]));

		if (words[1].Equals("GetColumns", StringComparison.InvariantCultureIgnoreCase)) {
			return string.Join(" ", columns.Select(c => string.Join(" ", c) + " end"));
		}

		if (words[1].Equals("Keypad", StringComparison.InvariantCultureIgnoreCase)) {
			// Stage 1
			var glyphs = words.Skip(2).Select(s => (Glyph) Enum.Parse(typeof(Glyph), s, true));
			var numColumns = columns.Count(c => glyphs.All(c.Contains));
			switch (numColumns) {
				case 0: return "NoColumn";
				case > 1: return "MultipleColumns";
				default:
					var column = columns.First(column => glyphs.All(column.Contains));
					return string.Join(" ", column.Where(glyphs.Contains));
			}
		}

		if (words[1].Equals("CheckKeypad", StringComparison.InvariantCultureIgnoreCase)) {
			// Stage 1
			var glyphs = words.Skip(2).Where(s => s != "unknown").Select(s => (Glyph) Enum.Parse(typeof(Glyph), s, true));
			return columns.Any(column => glyphs.All(column.Contains)) ? "true" : "false";
		}

		List<int>? possibleColumns = null;
		if (process.User.Predicates.TryGetValue("KeypadColumns", out var ruledColumnsString))
			possibleColumns = ruledColumnsString.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();

		// Process the response.
		if (words[1].Equals("Start", StringComparison.InvariantCultureIgnoreCase)) {
			// Stage 2
			// If any glyphs are known, skip to stage 3.
			var glyphs = new List<Glyph>();
			for (var i = 1; ; ++i) {
				if (!process.User.Predicates.TryGetValue("KeypadGlyph" + i, out var glyphString))
					break;
				glyphs.Add((Glyph) Enum.Parse(typeof(Glyph), glyphString, true));
			}

			if (glyphs.Count > 0) {
				process.User.Predicates["KeypadStage"] = "3";
				possibleColumns = Enumerable.Range(0, 6).Where(i => glyphs.All(g => columns[i].Contains(g))).ToList();
				process.User.Predicates["KeypadColumns"] = string.Join(" ", possibleColumns);
			}
		} else {
			var boolString = words[2];
			var glyphName = words[3];
			process.User.Predicates["KeypadAsked"] = process.User.Predicates.TryGetValue("KeypadAsked", out var asked1) && asked1 != ""
				? asked1 + " " + glyphName
				: glyphName;

			var glyph = (Glyph) Enum.Parse(typeof(Glyph), glyphName, true);

			if (bool.Parse(boolString)) {
				if (possibleColumns == null) {
					possibleColumns = Enumerable.Range(0, 6).Where(i => columns[i].Contains(glyph)).ToList();
					process.User.Predicates["KeypadColumns"] = string.Join(" ", possibleColumns);
				} else {
					possibleColumns.RemoveAll(i => !columns[i].Contains(glyph));
				}
			}
		}

		// Now figure out what to ask next.
		if (possibleColumns == null) {
			// Stage 2: Start by asking about glyphs that are in two columns.
			// If they say yes, we know that the correct column must be one of those columns.

			// Choose a random glyph that hasn't already been asked.
			var glyphs = GetGlyphsWithFrequency(columns, 2).ToList();
			if (process.User.Predicates.TryGetValue("KeypadAsked", out var asked1) && asked1 != "") {
				foreach (var glyph in from s in asked1.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries) where s != "unknown"
									  select (Glyph) Enum.Parse(typeof(Glyph), s, true)) {
					glyphs.Remove(glyph);
				}
			}

			if (glyphs.Count > 0)
				return "Ask " + Pick(glyphs);

			// It is possible that we will run out of doubled glyphs. In this case, start asking about other glyphs.
			glyphs = GetGlyphsWithFrequency(columns, 1).ToList();
			if (process.User.Predicates.TryGetValue("KeypadAsked", out asked1) && asked1 != "") {
				foreach (var glyph in from s in asked1.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries) where s != "unknown"
									  select (Glyph) Enum.Parse(typeof(Glyph), s, true)) {
					glyphs.Remove(glyph);
				}
			}

			return glyphs.Count > 0 ? "Ask " + Pick(glyphs) : throw new InvalidOperationException("Ruled out every glyph in stage 2?!");
		}

		if (possibleColumns.Count == 1) {
			return "Tell " + string.Join(" ", columns[possibleColumns[0]]);
		}

		{
			// Stage 3: try to figure out which column is the correct one by finding a glyph only in one.
			var glyphs = new List<Glyph>();

			var askedGlyphs = new HashSet<Glyph>();
			if (process.User.Predicates.TryGetValue("KeypadAsked", out var asked1) && asked1 != "") {
				foreach (var glyph in from s in asked1.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries) where s != "unknown"
					select (Glyph) Enum.Parse(typeof(Glyph), s, true)) {
					askedGlyphs.Add(glyph);
				}
			}

			foreach (var columnIndex in possibleColumns) {
				glyphs.AddRange(columns[columnIndex].Where(g => !askedGlyphs.Contains(g) && !possibleColumns.Any(i => i != columnIndex && columns[i].Contains(g))));
			}

			return glyphs.Count > 0 ? "Ask " + Pick(glyphs) : throw new InvalidOperationException("Ruled out every glyph in stage 3?!");
		}
	}

	private static T Pick<T>(IList<T> list) => list[new Random().Next(list.Count)];

	private static IEnumerable<Glyph> GetGlyphsWithFrequency(ICollection<IEnumerable<Glyph>> columns, int frequency) {
		for (var glyph = Copyright; glyph <= BT; ++glyph) {
			if (columns.Count(c => c.Contains(glyph)) == frequency) yield return glyph;
		}
	}

	public void GenerateAiml(string path, int ruleSeed) {
		var columns = GetColumns(ruleSeed);

		using (var writer = new StreamWriter(Path.Combine(path, "aiml", $"keypad{ruleSeed}.aiml"))) {
			writer.WriteLine("<?xml version='1.0' encoding='UTF-8'?>");
			writer.WriteLine("<aiml version='2.0'>");
			for (var i = 1; i <= 2; ++i) {
				writer.WriteLine($"<category><pattern>XGlyphs {ruleSeed} {i}</pattern>");
				writer.WriteLine($"<template>{string.Join(" ", GetGlyphsWithFrequency(columns, i))}</template>");
				writer.WriteLine("</category>");
			}
			writer.WriteLine("</aiml>");
		}
		using (var writer = new StreamWriter(Path.Combine(path, "maps", $"KeypadColumns{ruleSeed}.txt"))) {
			for (var i = 0; i < columns.Length; ++i) {
				writer.WriteLine($"{i + 1}:{string.Join(" ", columns[i])}");
			}
		}
	}

	// Glyph names are from http://www.bombmanual.com
	[SuppressMessage("ReSharper", "InconsistentNaming")]
	public enum Glyph {
		Copyright = 1,  // '©'
		FilledStar,     // '★'
		HollowStar,     // '☆'
		SmileyFace,     // 'ټ'
		DoubleK,        // 'Җ'
		Omega,          // 'Ω'
		SquidKnife,     // 'Ѭ'
		Pumpkin,        // 'ѽ'
		HookN,          // 'ϗ'
		Teepee,         // 'ϫ'
		Six,            // 'Ϭ'
		SquigglyN,      // 'Ϟ'
		AT,             // 'Ѧ'
		Ae,             // 'æ'
		MeltedThree,    // 'Ԇ'
		Euro,           // 'Ӭ'
		Circle,         // '҈'
		NWithHat,       // 'Ҋ'
		Dragon,         // 'Ѯ'
		QuestionMark,   // '¿'
		Paragraph,      // '¶'
		RightC,         // 'Ͼ'
		LeftC,          // 'Ͽ'
		Pitchfork,      // 'Ψ'
		Tripod,         // 'Ѫ'
		Cursive,        // 'Ҩ'
		Tracks,         // '҂'
		Balloon,        // 'Ϙ'
		WeirdNose,      // 'ζ'
		UpsideDownY,    // 'ƛ'
		BT              // 'Ѣ'
	}
}
