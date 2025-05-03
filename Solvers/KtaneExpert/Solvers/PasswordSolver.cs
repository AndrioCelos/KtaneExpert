using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AngelAiml;

namespace KtaneExpert.Solvers;
public class PasswordSolver : IModuleSolver {
	private static readonly string[] vanillaPasswords = [
		"about", "after", "again", "below", "could",
		"every", "first", "found", "great", "house",
		"large", "learn", "never", "other", "place",
		"plant", "point", "right", "small", "sound",
		"spell", "still", "study", "their", "there",
		"these", "thing", "think", "three", "water",
		"where", "which", "world", "would", "write"
	];
	private static readonly string[] passwords = [
		"there", "which", "their", "other", "about", "these", "would",
		"write", "could", "first", "water", "sound", "place", "after",
		"thing", "think", "great", "where", "right", "three", "small",
		"large", "again", "spell", "house", "point", "found", "study",
		"still", "learn", "world", "every", "below", "plant", "never",

		"aback", "abbey", "abbot", "above", "abuse", "acids", "acres", "acted", "actor", "acute", "adapt", "added", "admit", "adopt", "adult", "agent", "agony", "agree",
		"ahead", "aided", "aimed", "aisle", "alarm", "album", "alert", "algae", "alien", "alike", "alive", "alley", "allow", "alloy", "alone", "along", "aloof", "aloud",
		"alpha", "altar", "alter", "amend", "amino", "among", "ample", "angel", "anger", "angle", "angry", "ankle", "apart", "apple", "apply", "apron", "areas", "arena",
		"argue", "arise", "armed", "aroma", "arose", "array", "arrow", "arson", "ashes", "aside", "asked", "assay", "asset", "atoms", "attic", "audio", "audit", "avoid",
		"await", "awake", "award", "aware", "awful", "awoke", "backs", "bacon", "badge", "badly", "baked", "baker", "balls", "bands", "banks", "barge", "baron", "basal",
		"based", "bases", "basic", "basin", "basis", "batch", "baths", "beach", "beads", "beams", "beans", "beard", "bears", "beast", "beech", "beers", "began", "begin",
		"begun", "being", "bells", "belly", "belts", "bench", "bible", "bikes", "bills", "birds", "birth", "black", "blade", "blame", "bland", "blank", "blast",
		"blaze", "bleak", "bleat", "blend", "bless", "blind", "block", "bloke", "blond", "blood", "bloom", "blown", "blows", "blues", "blunt", "board", "boats", "bogus",
		"bolts", "bombs", "bonds", "bones", "bonus", "books", "boost", "boots", "bored", "borne", "bound", "bowed", "bowel", "bowls", "boxed", "boxer", "boxes", "brain",
		"brake", "brand", "brass", "brave", "bread", "break", "bream", "breed", "brick", "bride", "brief", "bring", "brink", "brisk", "broad", "broke", "broom", "brown",
		"brows", "brush", "build", "built", "bulbs", "bulky", "bulls", "bunch", "bunny", "burns", "burnt", "burst", "buses", "buyer", "cabin", "cable", "cache", "cakes",
		"calls", "camps", "canal", "candy", "canoe", "canon", "cards", "cared", "carer", "cares", "cargo", "carry", "cases", "catch", "cater", "cause", "caves", "cease",
		"cells", "cents", "chain", "chair", "chalk", "chaos", "chaps", "charm", "chart", "chase", "cheap", "check", "cheek", "cheer", "chess", "chest", "chief", "child",
		"chill", "china", "chips", "choir", "chord", "chose", "chunk", "cider", "cigar", "cited", "cites", "civic", "civil", "claim", "clash", "class", "claws", "clean",
		"clear", "clerk", "click", "cliff", "climb", "cloak", "clock", "close", "cloth", "cloud", "clown", "clubs", "cluck", "clues", "clung", "coach", "coast", "coats",
		"cocoa", "codes", "coins", "colon", "comes", "comic", "coral", "corps", "costs", "couch", "cough", "count", "court", "cover", "crack", "craft", "crane", "crash",
		"crate", "crazy", "cream", "creed", "crept", "crest", "crews", "cried", "cries", "crime", "crisp", "crops", "cross", "crowd", "crown", "crude", "cruel", "crust",
		"crypt", "cubic", "curls", "curly", "curry", "curse", "curve", "cycle", "daddy", "daily", "dairy", "dance", "dared", "dated", "dates", "deals", "dealt", "death",
		"debit", "debts", "debut", "decay", "decor", "decoy", "deeds", "deity", "delay", "dense", "depot", "depth", "derby", "derry", "desks", "deter", "devil", "diary",
		"diets", "dimly", "dirty", "disco", "discs", "disks", "ditch", "dived", "dizzy", "docks", "dodgy", "doing", "dolls", "donor", "doors", "doses", "doubt", "dough",
		"downs", "dozen", "draft", "drain", "drama", "drank", "drawn", "draws", "dread", "dream", "dress", "dried", "drift", "drill", "drily", "drink", "drive", "drops",
		"drove", "drown", "drugs", "drums", "drunk", "duchy", "ducks", "dunes", "dusty", "dutch", "dwarf", "dying", "eager", "eagle", "early", "earth", "eased", "eaten",
		"edges", "eerie", "eight", "elbow", "elder", "elect", "elite", "elves", "empty", "ended", "enemy", "enjoy", "enter", "entry", "envoy", "equal", "erect", "error",
		"essay", "ethos", "event", "exact", "exams", "exert", "exile", "exist", "extra", "faced", "faces", "facts", "faded", "fails", "faint", "fairs", "fairy", "faith",
		"falls", "false", "famed", "fancy", "fares", "farms", "fatal", "fatty", "fault", "fauna", "fears", "feast", "feels", "fella", "fence", "feret", "ferry", "fetal",
		"fetch", "fever", "fewer", "fibre", "field", "fiery", "fifth", "fifty", "fight", "filed", "files", "fills", "films", "final", "finds", "fined", "finer", "fines",
		"fired", "fires", "firms", "fists", "fiver", "fixed", "flags", "flair", "flame", "flank", "flash", "flask", "flats", "flaws", "fleet", "flesh", "flies", "float",
		"flock", "flood", "floor", "flora", "flour", "flown", "flows", "fluid", "flung", "flunk", "flush", "flute", "focal", "focus", "folds", "folks", "folly", "fonts",
		"foods", "fools", "force", "forms", "forth", "forty", "forum", "fours", "foxes", "foyer", "frail", "frame", "franc", "frank", "fraud", "freak", "freed", "fresh",
		"fried", "frogs", "front", "frost", "frown", "froze", "fruit", "fuels", "fully", "fumes", "funds", "funny", "gains", "games", "gangs", "gases", "gates", "gauge",
		"gazed", "geese", "genes", "genre", "genus", "ghost", "giant", "gifts", "girls", "given", "gives", "glare", "glass", "gleam", "globe", "gloom", "glory", "gloss",
		"glove", "goals", "goats", "going", "goods", "goose", "gorge", "grace", "grade", "grain", "grand", "grant", "graph", "grasp", "grass", "grave", "greed", "greek",
		"green", "greet", "grief", "grill", "grips", "groom", "gross", "group", "grown", "grows", "guard", "guess", "guest", "guide", "guild", "guilt", "guise",
		"gulls", "gully", "gypsy", "habit", "hairs", "hairy", "halls", "hands", "handy", "hangs", "happy", "hardy", "harsh", "haste", "hasty", "hatch", "hated", "hates",
		"haven", "havoc", "heads", "heady", "heard", "hears", "heart", "heath", "heavy", "hedge", "heels", "hefty", "heirs", "hello", "helps", "hence", "henry", "herbs",
		"herds", "hills", "hints", "hired", "hobby", "holds", "holes", "holly", "homes", "honey", "hooks", "hoped", "hopes", "horns", "horse", "hosts", "hotel", "hours",
		"human", "humus", "hurry", "hurts", "hymns", "icing", "icons", "ideal", "ideas", "idiot", "image", "imply", "index", "india", "inert", "infer", "inner", "input",
		"irony", "issue", "items", "ivory", "japan", "jeans", "jelly", "jewel", "joins", "joint", "joker", "jokes", "jolly", "joule", "joust", "judge", "juice",
		"keeps", "kicks", "kills", "kinds", "kings", "knees", "knelt", "knife", "knobs", "knock", "knots", "known", "knows", "label", "lacks", "lager", "lakes", "lambs",
		"lamps", "lands", "lanes", "laser", "lasts", "later", "laugh", "lawns", "layer", "leads", "leant", "leapt", "lease", "least", "leave", "ledge", "legal", "lemon",
		"level", "lever", "libel", "lifts", "light", "liked", "likes", "limbs", "limit", "lined", "linen", "liner", "lines", "links", "lions", "lists", "litre", "lived",
		"liver", "lives", "loads", "loans", "lobby", "local", "locks", "locus", "lodge", "lofty", "logic", "looks", "loops", "loose", "lords", "lorry", "loser", "loses",
		"lotus", "loved", "lover", "loves", "lower", "loyal", "lucky", "lumps", "lunch", "lungs", "lying", "macho", "madam", "magic", "mains", "maize", "major", "maker",
		"makes", "males", "manor", "march", "marks", "marry", "marsh", "masks", "match", "mates", "maths", "maybe", "mayor", "meals", "means", "meant", "medal", "media",
		"meets", "menus", "mercy", "merge", "merit", "merry", "messy", "metal", "meter", "metre", "micro", "midst", "might", "miles", "mills", "minds", "miner", "mines",
		"minor", "minus", "misty", "mixed", "model", "modem", "modes", "moist", "moles", "money", "monks", "month", "moods", "moors", "moral", "motif", "motor", "motto",
		"mould", "mound", "mount", "mouse", "mouth", "moved", "moves", "movie", "muddy", "mummy", "mused", "music", "myths", "nails", "naive", "named", "names",
		"nanny", "nasty", "naval", "necks", "needs", "nerve", "nests", "newer", "newly", "nicer", "niche", "niece", "night", "ninth", "noble", "nodes", "noise",
		"noisy", "nomes", "norms", "north", "noses", "noted", "notes", "novel", "nurse", "nutty", "nylon", "occur", "ocean", "oddly", "odour", "offer", "often", "older",
		"olive", "onion", "onset", "opens", "opera", "orbit", "order", "organ", "ought", "ounce", "outer", "overs", "overt", "owned", "owner", "oxide", "ozone", "packs",
		"pages", "pains", "paint", "pairs", "palms", "panel", "panic", "pants", "papal", "paper", "parks", "parts", "party", "pasta", "paste", "patch", "paths", "patio",
		"pause", "peace", "peaks", "pearl", "pears", "peers", "penal", "pence", "penny", "pests", "petty", "phase", "phone", "photo", "piano", "picks", "piece", "piers",
		"piled", "piles", "pills", "pilot", "pinch", "pints", "pious", "pipes", "pitch", "pizza", "plain", "plane", "plans", "plate", "plays", "plead", "pleas", "plots",
		"plump", "poems", "poets", "polar", "poles", "polls", "ponds", "pools", "porch", "pores", "ports", "posed", "poses", "posts", "pound", "power", "press", "price",
		"pride", "prime", "print", "prior", "privy", "prize", "probe", "prone", "proof", "prose", "proud", "prove", "proxy", "pulls", "pulse", "pumps", "punch", "pupil",
		"puppy", "purse", "quack", "queen", "query", "quest", "queue", "quick", "quiet", "quite", "quota", "quote", "raced", "races", "radar", "radio", "raids", "rails",
		"raise", "rally", "range", "ranks", "rapid", "rated", "rates", "ratio", "razor", "reach", "react", "reads", "ready", "realm", "rebel", "refer", "reign",
		"reins", "relax", "remit", "renal", "renew", "rents", "repay", "reply", "resin", "rests", "rider", "ridge", "rifle", "rigid", "rings", "riots", "risen", "rises",
		"risks", "risky", "rites", "rival", "river", "roads", "robes", "robot", "rocks", "rocky", "rogue", "roles", "rolls", "roman", "roofs", "rooms", "roots", "ropes",
		"roses", "rotor", "rouge", "rough", "round", "route", "rover", "royal", "rugby", "ruins", "ruled", "ruler", "rules", "rural", "rusty", "sadly", "safer", "sails",
		"saint", "salad", "sales", "salon", "salts", "sands", "sandy", "satin", "sauce", "saved", "saves", "scale", "scalp", "scant", "scarf", "scars", "scene", "scent",
		"scoop", "scope", "score", "scots", "scrap", "screw", "scrum", "seals", "seams", "seats", "seeds", "seeks", "seems", "seize", "sells", "sends", "sense", "serum",
		"serve", "seven", "sexes", "shade", "shady", "shaft", "shake", "shaky", "shall", "shame", "shape", "share", "sharp", "sheep", "sheer", "sheet", "shelf", "shell",
		"shift", "shiny", "ships", "shire", "shirt", "shock", "shoes", "shone", "shook", "shoot", "shops", "shore", "short", "shots", "shout", "shown", "shows", "shrug",
		"sides", "siege", "sight", "signs", "silly", "since", "sings", "sites", "sixth", "sixty", "sizes", "skies", "skill", "skins", "skirt", "skull", "slabs", "slate",
		"slave", "sleek", "sleep", "slept", "slice", "slide", "slope", "slots", "slump", "smart", "smell", "smile", "smoke", "snake", "sober", "socks", "soils", "solar",
		"solid", "solve", "songs", "sorry", "sorts", "souls", "south", "space", "spade", "spare", "spark", "spate", "spawn", "speak", "speed", "spend", "spent", "spies",
		"spine", "splat", "split", "spoil", "spoke", "spoon", "sport", "spots", "spray", "spurs", "squad", "stack", "staff", "stage", "stain", "stair", "stake", "stale",
		"stall", "stamp", "stand", "stare", "stark", "stars", "start", "state", "stays", "steak", "steal", "steam", "steel", "steep", "steer", "stems", "steps", "stern",
		"stick", "stiff", "sting", "stock", "stole", "stone", "stony", "stood", "stool", "stops", "store", "storm", "story", "stout", "stove", "strap", "straw", "stray",
		"strip", "stuck", "stuff", "style", "suede", "sugar", "suite", "suits", "sunny", "super", "surge", "swans", "swear", "sweat", "sweep", "sweet", "swept", "swift",
		"swing", "swiss", "sword", "swore", "sworn", "swung", "table", "tacit", "tails", "taken", "takes", "tales", "talks", "tanks", "tapes", "tasks", "taste", "tasty",
		"taxed", "taxes", "taxis", "teach", "teams", "tears", "teddy", "teens", "teeth", "tells", "telly", "tempo", "tends", "tenor", "tense", "tenth", "tents", "terms",
		"tests", "texas", "texts", "thank", "theft", "theme", "thick", "thief", "thigh", "third", "those", "threw", "throw", "thumb", "tidal", "tides", "tiger", "tight",
		"tiles", "times", "timid", "tired", "title", "toast", "today", "token", "tones", "tonic", "tonne", "tools", "toons", "tooth", "topic", "torch", "total", "touch",
		"tough", "tours", "towel", "tower", "towns", "toxic", "trace", "track", "tract", "trade", "trail", "train", "trait", "tramp", "trams", "trays", "treat", "trees",
		"trend", "trial", "tribe", "trick", "tried", "tries", "trips", "troop", "trout", "truce", "truck", "truly", "trunk", "trust", "truth", "tubes", "tummy", "tunes",
		"tunic", "turks", "turns", "tutor", "twice", "twins", "twist", "tying", "types", "tyres", "ulcer", "unban", "uncle", "under", "undue", "unfit", "union", "unite",
		"units", "unity", "until", "upper", "upset", "urban", "urged", "urine", "usage", "users", "using", "usual", "utter", "vague", "valid", "value", "valve", "vault",
		"veins", "venue", "verbs", "verge", "verse", "vicar", "video", "views", "villa", "vines", "vinyl", "virus", "visit", "vital", "vivid", "vocal", "vodka", "voice",
		"voted", "voter", "votes", "vowed", "vowel", "wages", "wagon", "waist", "waits", "walks", "walls", "wants", "wards", "wares", "warns", "waste", "watch", "waved",
		"waves", "wears", "weary", "wedge", "weeds", "weeks", "weigh", "weird", "wells", "welsh", "whale", "wheat", "wheel", "while", "white", "whole", "whose", "widen",
		"wider", "widow", "width", "wills", "winds", "windy", "wines", "wings", "wiped", "wires", "wiser", "witch", "witty", "wives", "woken", "woman", "women", "woods",
		"words", "works", "worms", "worry", "worse", "worst", "worth", "wound", "woven", "wrath", "wreck", "wrist", "wrong", "wrote", "wryly", "xerox", "yacht", "yards",
		"yawns", "years", "yeast", "yield", "young", "yours", "youth", "zilch", "zones"
	];

	private static readonly Cache<int, string[]> cache = new(64, GetWordsInternal);

	public static string[] GetWords(int ruleSeed) => cache.Get(ruleSeed);
	private static string[] GetWordsInternal(int ruleSeed) {
		if (ruleSeed == 1) return (string[]) vanillaPasswords.Clone();

		var random = new MonoRandom(ruleSeed);

		// Start with 17 random words from the list.
		var words1 = new string[passwords.Length];
		Array.Copy(passwords, words1, passwords.Length);
		words1 = random.Shuffle(words1);

		var words = new string[35];
		Array.Copy(words1, words, 17);

		// Add more words that are similar to the base words.
		var count = 17;
		for (var i = 0; i < 17 && count < 35; i++) {
			var toAdd = (from w in passwords where !words.Contains(w)
						 select (word: w, pref: random.NextDouble(), dist: HammingDistance(w, words[i]))).ToArray();
			Array.Sort(toAdd, (a, b) => {
				var r = a.dist - b.dist;
				return r == 0 ? a.pref.CompareTo(b.pref) : r;
			});

			var numberToAdd = Math.Min(random.Next(1, 5), 35 - count);
			for (var j = 0; j < numberToAdd; ++j) {
				words[count] = toAdd[j].word;
				++count;
			}
		}
		Array.Sort(words);
		return words;
	}

	private static int HammingDistance(string a, string b) => a.Where((t, i) => i >= b.Length || b[i] != t).Count();

	public void GenerateAiml(string path, int ruleSeed) {
		var words = GetWords(ruleSeed);

		using var writer = new StreamWriter(Path.Combine(path, "aiml", $"password{ruleSeed}.aiml"));
		writer.WriteLine("<?xml version='1.0' encoding='UTF-8'?>");
		writer.WriteLine("<aiml version='2.0'>");
		writer.WriteLine($"<category><pattern>SolverFallback Password {ruleSeed} GetPasswords</pattern>");
		writer.WriteLine($"<template>{string.Join(" ", words)}</template>");
		writer.WriteLine("</category>");
		writer.WriteLine("</aiml>");
	}

	public string Process(string text, XElement element, RequestProcess process) {
		var words = text.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
		if (words[1].Equals("GetPasswords", StringComparison.CurrentCultureIgnoreCase)) {
			return string.Join(" ", GetWords(int.Parse(words[0])));
		}

		var possiblePasswords = new List<string>(GetWords(int.Parse(words[0])));

		for (var i = 0; i < 5; ++i) {
			var found = process.User.Predicates.TryGetValue("Password" + i, out var s) && s != "" && s != "unknown";
			if (!found) {
				s = text;
				process.User.Predicates["Password" + i] = s;
			}
			var chars = s!.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries).Select(s => char.ToLowerInvariant(s[0])).ToList();
			possiblePasswords.RemoveAll(p => !chars.Contains(p[i]));

			switch (possiblePasswords.Count) {
				case 0:
					return "No passwords match. Please try again.";
				case 1:
					return "The password is: " + possiblePasswords[0];
			}

			if (!found) {
				return "Tell me the letters for the next position.";
			}
		}

		return "Couldn't find the password. Please try again.";
	}
}
