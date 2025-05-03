using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AngelAiml;

namespace KtaneExpert.Solvers;
public partial class WhosOnFirstSolver : IModuleSolver {
	private static readonly string[] displayTexts = [
		"YES", "FIRST", "DISPLAY", "OKAY", "SAYS", "NOTHING", "", "BLANK", "NO", "LED",
		"LEAD", "READ", "RED", "REED", "LEED", "HOLD ON", "YOU", "YOU ARE", "YOUR",
		"YOU'RE", "UR", "THERE", "THEY'RE", "THEIR", "THEY ARE", "SEE", "C", "CEE"
	];
	private static readonly string[][] buttonTexts = [
		["READY", "FIRST", "NO", "BLANK", "NOTHING", "YES", "WHAT", "UHHH", "LEFT", "RIGHT", "MIDDLE", "OKAY", "WAIT", "PRESS"],
		["YOU", "YOU ARE", "YOUR", "YOU'RE", "UR", "U", "UH HUH", "UH UH", "WHAT?", "DONE", "NEXT", "HOLD", "SURE", "LIKE"]
	];

	[GeneratedRegex(@"\W")]
	private static partial Regex SymbolRegex();

	public static RuleSet GetRules(int ruleSeed) {
		var random = new MonoRandom(ruleSeed);

		var displayRules = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
		var buttonRules = new Dictionary<string, string[]>(StringComparer.CurrentCultureIgnoreCase);

		foreach (var word in displayTexts)
			displayRules[word] = random.Next(6);

		for (var i = 0; i < buttonTexts.Length; ++i) {
			for (var j = 0; j < buttonTexts[i].Length; ++j) {
				var array = (string[]) buttonTexts[i].Clone();
				random.ShuffleFisherYates(array);
				buttonRules[buttonTexts[i][j]] = array;
			}
		}

		return new(displayRules, buttonRules);
	}

	public void GenerateAiml(string path, int ruleSeed) {
		var rules = GetRules(ruleSeed);

		using (var writer = new StreamWriter(Path.Combine(path, "maps", $"WhosOnFirstButton{ruleSeed}.txt"))) {
			foreach (var entry in rules.DisplayRules) {
				var value = entry.Value switch {
					0 => "top left",
					1 => "top right",
					2 => "middle left",
					3 => "middle right",
					4 => "bottom left",
					5 => "bottom right",
					_ => throw new InvalidOperationException("Unknown button position")
				};
				writer.WriteLine($"{AimlEncode(entry.Key)}:{value}");
			}
		}

		using (var writer = new StreamWriter(Path.Combine(path, "maps", $"WhosOnFirstList{ruleSeed}.txt"))) {
			foreach (var entry in rules.ButtonRules) {
				writer.Write($"{AimlEncode(entry.Key)}:");
				foreach (var label in entry.Value) {
					writer.Write(AimlEncode(label));
					if (label == entry.Key) break;
					writer.Write(' ');
				}
				writer.WriteLine();
			}
		}
	}

	public string Process(string text, XElement element, RequestProcess process) {
		// Usage: <rule seed> (Display|Button) <text>

		try {
			var words = text.Split((char[]?) null, 3, StringSplitOptions.RemoveEmptyEntries);
			var rules = GetRules(int.Parse(words[0]));
			var label = AimlDecode(words.Length > 2 ? words[2] : "empty");

			if (words[1].Equals("Display", StringComparison.CurrentCultureIgnoreCase)) {
				var result = rules.DisplayRules[label];
				return result switch {
					0 => "top left",
					1 => "top right",
					2 => "middle left",
					3 => "middle right",
					4 => "bottom left",
					5 => "bottom right",
					_ => throw new InvalidOperationException("Unknown button position"),
				};
			} else {
				var builder = new StringBuilder();
				var result = rules.ButtonRules[label];
				foreach (var label2 in result) {
					if (builder.Length > 0) builder.Append(' ');
					builder.Append(AimlEncode(label2));

					if (label2 == label) break;
				}
				return builder.ToString();
			}
		} catch (KeyNotFoundException) {
			return "unknown";
		}
	}

	private static string AimlEncode(string text) => text != "" ? SymbolRegex().Replace(text, "_") : "empty";

	private static string AimlDecode(string text) => text switch {
		"empty" => "",
		"HOLD_ON" => "HOLD ON",
		"THEY_ARE" => "THEY ARE",
		"THEY_RE" => "THEY'RE",
		"WHAT_" => "WHAT?",
		"UH_HUH" => "UH HUH",
		"UH_UH" => "UH UH",
		"YOU_ARE" => "YOU ARE",
		"YOU_RE" => "YOU'RE",
		_ => text
	};

	public record RuleSet(Dictionary<string, int> DisplayRules, Dictionary<string, string[]> ButtonRules);
}
