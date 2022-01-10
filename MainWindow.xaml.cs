using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Markup;

namespace BabbleTrainer
{
    public class BabTrainer
    {
        public class Word
        {
            public string word;
            public string type;
            public string definition;
        }
        public static Dictionary<string, Word> words = new Dictionary<string, Word>(300000);

        public class Node
        {
            public char letter;
            public Word word;
            public Node parent;
            public Node[] children = new Node[26];
        }

        public static Node root = new Node();
        public const int asciiOffset = 97;

        public Word GetWordInfo(string word)
        {
            Node currentNode = root;
            for (int i = 0; i < word.Length; i++)
            {
                int currentLetter = word[i] - asciiOffset;
                if (currentNode.children[currentLetter] != null)
                    currentNode = currentNode.children[currentLetter];
                if (currentNode.word != null && currentNode.word.word == word)
                    return currentNode.word;
            }
            return null;
        }

        public Word GetRandomRemaining(int minLength)
        {
            Dictionary<(int, int), HashSet<string>> intToSet = new Dictionary<(int, int), HashSet<string>>();
            int counter = 0;
            foreach (var set in remainingWordsByLength)
            {
                if (set.Key >= minLength)
                {
                    intToSet[(counter, counter + set.Value.Count)] = set.Value;
                    counter += set.Value.Count;
                }
            }
            int index = r.Next(0, counter);

            foreach (var pointer in intToSet)
            {
                if (index >= pointer.Key.Item1 && index < pointer.Key.Item2)
                    return words[pointer.Value.ToList()[r.Next(0, pointer.Value.Count)]];
            }

            return null;
        }

        public static int[] letterFreq = new int[26];
        public Random r = new Random();
        public static int restrictionCountMin = 2;
        public static int restrictionCountMax = 4;
        public static int lineCount = 1;
        public static int lineLength = 1;
        public static int minFixed = 1;
        public static int minBridgeLength = 1;
        public static int lineLengthFactor = 1;
        public int[] bag;
        public int bagSize = 7;
		public int seed;

        public StringBuilder restrictionText = new StringBuilder();
        public HashSet<string> validWords = new HashSet<string>(500);
        public HashSet<string> remainingWords = new HashSet<string>(500);
        public HashSet<string> foundWords = new HashSet<string>(200);
        public (int, int)[][] restrictionSets;
        public Dictionary<int, int> validWordCountByLength = new Dictionary<int, int>(10);
        public Dictionary<int, HashSet<string>> remainingWordsByLength = new Dictionary<int, HashSet<string>>(10);
        public int[] emptyBag;
        public static int[] requiredLetters;

        public void NewGame(int gameSeed)
        {
            seed = gameSeed;
            r = new Random(seed);
            validWords.Clear();
            remainingWords.Clear();
            remainingWordsByLength.Clear();
            validWordCountByLength.Clear();
            foundWords.Clear();
            permaBag.Clear();

			FillBag(bag);

            if (lineCount != restrictionSets.Length)
                restrictionSets = new (int, int)[lineCount][];
            else
            {
                for (int i = 0; i < restrictionSets.Length; i++)
                    restrictionSets[i] = null;
            }

            for (int i = 0; i < lineCount; i++)
            {
                int restrictionCount = r.Next(restrictionCountMin, restrictionCountMax + 1);
                (int, int)[] restrictions = new (int, int)[restrictionCount];
                
                int[] restrictionLetters = GetRandomLetters(restrictionCount);
                for (int j = 0; j < restrictionCount; j++)
                {
                random:
                    int pos = (int)r.Next(0, lineLength);
                    for(int k = 0; k < restrictions.Length; k++)
                        if (restrictions[k].Item1 == pos)
                            goto random;
                    restrictions[j] = (pos, restrictionLetters[j]); ;
                }
                restrictionSets[i] = restrictions;
            }

            int sum = -1;
            for(int i = 0; i < requiredLetters.Length; i++)
            {
                bool contains = false;
                for(int j = 0; j < bagSize; j++)
                {
                    if (bag[j] == requiredLetters[i])
                    {
                        contains = true;
                        break;
                    }
                }
                if(!contains)
                {
                    for (int j = 0; j < restrictionSets.Length; j++)
                    {
                        for (int k = 0; k < restrictionSets[j].Length; k++)
                        {
                            if (restrictionSets[j][k].Item2 == requiredLetters[i])
                            {
                                contains = true;
                                break;
                            }
                        }
                        if (contains)
                            break;
                    }
                }
                if (!contains)
                {
                    if (sum == -1)
                    {
                        sum = bagSize;
                        for (int j = 0; j < restrictionSets.Length; j++)
                            sum += restrictionSets[j].Length;
                    }
                    int tries = 0;
                random:
                    tries++;
                    if (tries > 10)
                        return;
                    var rand = r.Next(0, sum);
                    int index = -1;
                    int counter = 0;
                    if (rand < bagSize)
                    {
                        int letterIndex = r.Next(0, bagSize);
                        for (int l = 0; l < requiredLetters.Length; l++)
                            if (bag[letterIndex] == requiredLetters[l])
                                goto random; // dont replace existing required letters
                        bag[letterIndex] = requiredLetters[i];
                    }
                    else
                    {
                        counter = bagSize;
                        for (int j = 0; j < restrictionSets.Length; j++)
                        {
                            counter += restrictionSets[j].Length;
                            if (rand < counter)
                            {
                                index = j;
                                break;
                            }
                        }
                        int letterIndex = r.Next(0, restrictionSets[index].Length);
                        for (int l = 0; l < requiredLetters.Length; l++)
                            if (restrictionSets[index][letterIndex].Item2 == requiredLetters[l])
                                goto random; // dont replace existing required letters
                        restrictionSets[index][letterIndex] = (restrictionSets[index][letterIndex].Item1, requiredLetters[i]);
                    }
                }
            }

            for(int setIndex = 0; setIndex < restrictionSets.Length; setIndex++)
            {
                (int, int)[] restrictions = restrictionSets[setIndex];

                for (int i = 2; i <= lineLength; i++)
                {
                    int restrictionShift = -(lineLength - i);
                    int minLength = 0;

                    if (minFixed != 0 || minBridgeLength != 0)
                    {
                        int totalFixed = 0;
                        int map = 0;
                        for (int j = 0; j < i; j++)
                        {
                            for (int k = 0; k < restrictions.Length; k++)
                            {
                                if (restrictions[k].Item1 + restrictionShift == j)
                                {
                                    totalFixed++;
                                    if (totalFixed == minFixed)
                                        minLength = j + 1;
                                    map |= 1 << j;
                                    break;
                                }
                            }
                        }
                        
                        int bridgeStart = -1;
                        int bridgeLength = 0;
                        int tileCount = 0;
                        int containedTileCount = 0;

                        for (int j = 0; j < i; j++)
                        {
                            if (bridgeStart != -1 && ((map >> j) & 1) != 1)
                                tileCount++;
                            if (bridgeStart == -1 && ((map >> j) & 1) == 1 && j + 1 < i && ((map >> (j + 1)) & 1) != 1)
                            {
                                // current is fixed, next isn't
                                bridgeStart = j;
                            }
                            if (bridgeStart != -1 && ((map >> j) & 1) != 1 && j + 1 < i && ((map >> (j + 1)) & 1) == 1)
                            {
                                // current is not fixed, next is
                                containedTileCount = tileCount;
                                bridgeLength = (j + 1) - bridgeStart + 1;
                                if (containedTileCount >= minBridgeLength && bridgeStart + bridgeLength > minLength)
                                {
                                    minLength = bridgeStart + bridgeLength;
                                    break;
                                }
                                else if (containedTileCount >= minBridgeLength)
                                {
                                    // constraint matched but other constraint requires longer word anyway
                                    break;
                                }
                            }
                        }

                        if (totalFixed < minFixed || containedTileCount < minBridgeLength)
                            continue;
                    }

                    GetValidPermutationsRestricted(bag, restrictions, minLength, validWords, null, 0, i, restrictionShift);
                }
            }

            remainingWords = new HashSet<string>(validWords);

            foreach (string word in remainingWords)
            {
                if (!remainingWordsByLength.ContainsKey(word.Length))
                {
                    validWordCountByLength[word.Length] = 0;
                    remainingWordsByLength[word.Length] = new HashSet<string>();
                }
                validWordCountByLength[word.Length]++;
                remainingWordsByLength[word.Length].Add(word);
            }
        }
        public void BuildRestrictionText()
        {
            restrictionText.Clear();
            var last = restrictionSets[restrictionSets.Length - 1];

            foreach (var res in restrictionSets)
            {
                for (int i = 0; i < lineLength; i++)
                {
                    bool written = false;

                    foreach (var f in res)
                    {
                        if (f.Item1 == i)
                        {
                            restrictionText.Append((char)(f.Item2 + asciiOffset));
                            written = true;
                            break;
                        }
                    }
                    if (!written)
                        restrictionText.Append("_");
                    restrictionText.Append(" ");
                }
                if (res != last)
                    restrictionText.Append("\n");
            }
        }

        public List<Inline> BuildRemainingText()
        {
            return BuildInfoText(remainingWords, false);
        }

        public List<Inline> BuildResultText(string[] constraintsA = null, string[] constraintsB = null)
        {
            List<Inline> result = new List<Inline>();
            result = BuildInfoText(remainingWords, true, constraintsA, constraintsB);
            result.Add(new Run("\n"));
            result.AddRange(BuildInfoText(foundWords, true, constraintsA, constraintsB));
            return result;
        }

        public List<Inline> BuildInfoText(HashSet<string> wordList, bool showWords, string[] constraintsA = null, string[] constraintsB = null)
        {
            Dictionary<int, List<string>> wordLengths = new Dictionary<int, List<string>>();
            Dictionary<int, int> wordLengthCount = new Dictionary<int, int>();
            List<Inline> inlines = new List<Inline>();
            int maxLength = 0;
            foreach (var word in wordList)
            {
                int l = word.Length;
                if (showWords)
                {
                    if (!wordLengths.ContainsKey(l))
                        wordLengths[l] = new List<string>();
                    wordLengths[l].Add(word);
                }
                if (!wordLengthCount.ContainsKey(l))
                    wordLengthCount[l] = 0;
                wordLengthCount[l]++;
                if (l > maxLength)
                    maxLength = l;
            }
            Run header = new Run(wordList.Count + " words");
            
            if (!showWords)
            {
                header.Text += " remaining";
            }
            if (showWords && wordList == foundWords)
            {
                header.Text += " found";
            }
            else if (showWords && wordList == remainingWords)
            {
                if (!usingWordList)
                    header.Text += " not found (hover for definition)";
                else
                    header.Text += " not found";
            }
            header.Text += "\n\n";

            inlines.Add(header);

            for (int i = 2; i <= maxLength; i++)
            {
                if (!wordLengthCount.ContainsKey(i))
                    continue;

                Run r = new Run();
                r.Text = wordLengthCount[i] + "x " + i.ToString() + "-letter words";
                if (showWords)
                {
                    r.Text += ": ";
                    r.SetValue(Run.FontWeightProperty, FontWeights.Bold);
                }
                inlines.Add(r);

                if (!showWords)
                {
                    int foundCount = validWordCountByLength[i] - remainingWordsByLength[i].Count;
                    int percentageFound = ((int)(((float)foundCount / (float)validWordCountByLength[i]) * 100f));
                    inlines.Add(new Run(" - " + foundCount.ToString() + " found - " + percentageFound.ToString() + "%"));
                }
                if (showWords)
                {
                    var words = wordLengths[i];
                    for (int k = 0; k < words.Count; k++)
                    {
                        string word = words[k];
                        Word wordInfo = BabTrainer.words[word];
                        if (wordInfo != null)
                        {
                            var run = new Run();
                            int a = 0;
                            foreach(string constraint in constraintsA)
                            {
                                if (constraint == "")
                                    continue;
                                if (word.Contains(constraint))
                                {
                                    run.SetValue(Run.FontWeightProperty, FontWeights.Bold);
                                    a++;
                                }
                            }
                            foreach (string constraint in constraintsB)
                            {
                                if (constraint == "")
                                    continue;
                                if (MainWindow.ContainsAllLetters(word, constraint))
                                {
                                    run.SetValue(Run.FontWeightProperty, FontWeights.Bold);
                                }
                            }
                                
                            run.Text = word + ((k != words.Count - 1) ? ", " : "");
                            if (!usingWordList)
                                run.ToolTip = word + " - " + wordInfo.type + " - " + HandleWordVariant(wordInfo.definition, false);
                            inlines.Add(run);
                        }
                    }
                }
                inlines.Add(new Run("\n"));
            }
            return inlines;
        }

        public static string dictLoadInfo;
        public static bool usingWordList = false;
        public static bool initialized = false;
                
        void SetLetters()
        {
            letterFreq['a' - asciiOffset] = 9;
            letterFreq['b' - asciiOffset] = 2;
            letterFreq['c' - asciiOffset] = 2;
            letterFreq['d' - asciiOffset] = 4;
            letterFreq['e' - asciiOffset] = 12;
            letterFreq['f' - asciiOffset] = 2;
            letterFreq['g' - asciiOffset] = 3;
            letterFreq['h' - asciiOffset] = 2;
            letterFreq['i' - asciiOffset] = 9;
            letterFreq['j' - asciiOffset] = 1;
            letterFreq['k' - asciiOffset] = 1;
            letterFreq['l' - asciiOffset] = 4;
            letterFreq['m' - asciiOffset] = 2;
            letterFreq['n' - asciiOffset] = 6;
            letterFreq['o' - asciiOffset] = 8;
            letterFreq['p' - asciiOffset] = 2;
            letterFreq['q' - asciiOffset] = 1;
            letterFreq['r' - asciiOffset] = 6;
            letterFreq['s' - asciiOffset] = 4;
            letterFreq['t' - asciiOffset] = 6;
            letterFreq['u' - asciiOffset] = 4;
            letterFreq['v' - asciiOffset] = 2;
            letterFreq['w' - asciiOffset] = 2;
            letterFreq['x' - asciiOffset] = 1;
            letterFreq['y' - asciiOffset] = 2;
            letterFreq['z' - asciiOffset] = 1;
        }

        public BabTrainer(int customBagSize)
        {
			bagSize = customBagSize;
            for(int i = 0; i < 20; i++)
                bufferBags[i] = new int[bagSize];

            bag = new int[bagSize];
            emptyBag = new int[bagSize];
            for (int i = 0; i < bagSize; i++)
                emptyBag[i] = 27;

            restrictionSets = new (int, int)[lineCount][];

            lineLength = (int)((1f + ((float)lineLengthFactor / 10f)) * bagSize) + (restrictionCountMin + restrictionCountMax) / 2;

            if (initialized)
                return;
            initialized = true;

            string babbleFolder = "";
            string path = babbleFolder + "BabbleRoyale_Data\\sharedassets0.assets";
            string dict = "";
            try
            {
                string f = File.ReadAllText(path, Encoding.UTF8);
                string startString = "aa~noun~A form";
                string endString = "zzzs~noun~Sleep.";
                int dictStart = f.IndexOf(startString);
                int dictEnd = f.LastIndexOf(endString) + endString.Length;
                if (dictStart == -1 || dictEnd == -1)
                    throw new Exception();
                int dictLength = dictEnd - dictStart;
                dict = f.Substring(dictStart, dictLength);
                usingWordList = false;
            }
            catch
            {
                dictLoadInfo = "Could not load dictionary, run in Babble Royale folder to load definitions.";
                try
                {
                    dict = File.ReadAllText("wordlist.txt");
                    usingWordList = true;
                }
                catch
                {
                    MessageBox.Show("Could not load dictionary from Babble Royale or wordlist.txt, run in Babble Royale folder to load definitions or supply wordlist.txt (https://github.com/wordnik/wordlist).", "BabbleTrainer", MessageBoxButton.OK, MessageBoxImage.Error);
                    System.Windows.Application.Current.Shutdown();
                    Environment.Exit(0);
                }
            }
			
            LoadDictionary(dict, usingWordList);
			SetLetters();
        }

        List<int> permaBag = new List<int>();

		public void FillBag(int[] bagToFill)
        {
            for (int i = 0; i < bagToFill.Length; i++)
            {
                if (permaBag.Count == 0)
                {
                    for(int k = 0; k < letterFreq.Length; k++)
                    {
                        for (int o = 0; o < letterFreq[k]; o++)
                            permaBag.Add(k);
                    }
                }

                int index = r.Next(0, permaBag.Count);
                bagToFill[i] = permaBag[index];
                permaBag.RemoveAt(index);
            }
        }

        public int[] GetRandomLetters(int count)
        {
            int[] results = new int[count];

            for (int i = 0; i < count; i++)
            {
                if (permaBag.Count == 0)
                {
                    for(int k = 0; k < letterFreq.Length; k++)
                    {
                        for (int o = 0; o < letterFreq[k]; o++)
                            permaBag.Add(k);
                    }
                }

                int index = r.Next(0, permaBag.Count);
                results[i] = permaBag[index];
                permaBag.RemoveAt(index);
            }
            return results;
        }

        int[][] bufferBags = new int[20][];

        public HashSet<string> GetValidPermutationsRestricted(int[] letters, (int, int)[] restrictions, int minLength, HashSet<string> results, Node currentNode = null, int currentLetter = 0, int maxLength = -1, int restrictionShift = 0)
        {
            if (currentNode == null)
                currentNode = root;

            bool nextLetterRestricted = false;

            for(int i = 0; i < restrictions.Length; i++)
            {
                int pos = restrictions[i].Item1 + restrictionShift;
                if (pos == currentLetter - 1)
                {
					// previous letter restricted
                    if (currentLetter == 0)					
						return results;
                }
                else if (pos == currentLetter)
                {
                    nextLetterRestricted = true;
                }
            }

            if (currentNode.word != null && !nextLetterRestricted && currentLetter >= minLength)
            {
                if (!results.Contains(currentNode.word.word))
                    results.Add(currentNode.word.word);
            }

            if (currentLetter == maxLength)
                return results;

            for (int i = 0; i < restrictions.Length; i++)
            {
                int pos = restrictions[i].Item1 + restrictionShift;
                if (pos == currentLetter)
                {
                    int restrictedLetter = restrictions[i].Item2;
                    if (currentNode.children[restrictedLetter] == null)
                        return results;
                    Node nextNode = currentNode.children[restrictedLetter];
                    int[] bag = bufferBags[currentLetter];
                    Array.Copy(letters, bag, letters.Length);
                    GetValidPermutationsRestricted(bag, restrictions, minLength, results, nextNode, currentLetter + 1, maxLength, restrictionShift);
                    return results;
                }
            }

            // only executed if no restriction on this letter
            {
                for (int i = 0; i < letters.Length; i++)
                {
                    if (letters[i] == 27)
                        continue;
                    if (currentNode.children[letters[i]] == null)
                        continue;
                    Node nextNode = currentNode.children[letters[i]];
                    int[] bag = bufferBags[currentLetter];
                    Array.Copy(letters, bag, letters.Length);
                    bag[i] = 27;
                    GetValidPermutationsRestricted(bag, restrictions, minLength, results, nextNode, currentLetter + 1, maxLength, restrictionShift);
                }
            }

            return results;
        }

        public List<Inline> BuildFoundInfo()
        {
            List<Inline> results = new List<Inline>();
            foreach (string word in foundWords.Reverse())
            {
                Word w = words[word];
                Run r = new Run();
                r.Text = w.word;
                r.SetValue(Run.FontWeightProperty, FontWeights.Bold);
                results.Add(r);
                Run t = new Run();
                if (!usingWordList)
                    t.Text = " - " + w.type + " - " + HandleWordVariant(w.definition, false) + "\n";
                else
                    t.Text = "\n";
                results.Add(t);
            }
            return results;
        }

        public static string[] keys = new string[] { "lural form of ", "lural of ", "ariant form of ",
            " spelling of ", "lternative form of ", "omparative form of ", "omparative of ",
            "ndicative form of ", "articiple of ", "ariant of ", "uperlative form of ",
            "uperlative of ", "imple past of " };

        public static (Word, int) GetBaseWord(string p)
        {
            int wordStart = -1;
            foreach (var key in keys)
            {
                wordStart = p.LastIndexOf(key);
                if (wordStart != -1)
                {
                    wordStart += key.Length;
                    break;
                }
            }
            if (wordStart == -1)
                return (null, -1);
            int wordEnd = -1;
            for (int i = wordStart; i < p.Length; i++)
            {
                if(p[i] == ' ' || p[i] == ';' || p[i] == '.' || p[i] == ':')
                {
                    wordEnd = i;
                    break;
                }
            }
            
            int singularLength = (wordEnd != -1 ? wordEnd : p.Length) - wordStart;
            string singular = p.Substring(wordStart, singularLength);
            if (!words.ContainsKey(singular))
                return (null, -1);
            return (words[singular], wordStart);
        }

        public static List<string> visited = new List<string>();

        public static string ReplaceLast(string text, string search, string replace)
        {
            int pos = text.LastIndexOf(search);
            if (pos < 0)
                return text;
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }

        public static string HandleWordVariant(string p, bool hide, List<string> v = null)
        {
            if (v == null)
            {
                visited.Clear();
                v = visited;
            }
            (Word s, int t) = GetBaseWord(p);
            if (s != null)
            {
                if (v.Contains(s.word))
                    return p;
                else
                    v.Add(s.word);
                string a = "";
                char next = p[p.LastIndexOf(s.word) + s.word.Length];
                if (next == '.' || next == ';' || next == ' ' || next == ':')
                    p = p.Remove(p.LastIndexOf(s.word) + s.word.Length, 1);
                if(hide)
                {
                    if (p.Length > t + s.word.Length + 4)
                        a = ReplaceLast(p, s.word, "_" + String.Concat(Enumerable.Repeat(" _", s.word.Length - 1)) + " - " + HandleWordVariant(s.definition, hide, v) + " - ");
                    else
                        a = ReplaceLast(p, s.word, "_" + String.Concat(Enumerable.Repeat(" _", s.word.Length - 1)) + " - " + HandleWordVariant(s.definition, hide, v));
                    a = a.Replace(s.word, "_" + String.Concat(Enumerable.Repeat(" _", s.word.Length - 1)));
                }
                else
                {
                    if (p.Length > t + s.word.Length + 4)
                        a = ReplaceLast(p, s.word, s.word + " - " + HandleWordVariant(s.definition, hide, v) + " - ");
                    else
                        a = ReplaceLast(p, s.word, s.word + " - " + HandleWordVariant(s.definition, hide, v));
                }

                if (a.EndsWith(". - ."))
                    a = a.Substring(0, a.Length - 5) + ".";
                if (a.EndsWith(" - .") || a.EndsWith(". - "))
                    a = a.Substring(0, a.Length - 4) + ".";

                return a;
            }
            else
                return p;
        }

        public void LoadDictionary(string dict, bool wordList)
        {
            var ss = new string[] { "\r\n", "\n" };
            string[] dict_words = dict.Split(ss, 9999999, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Dictionary<int, int> wordLengths = new Dictionary<int, int>();
            if (wordList)
            {
                foreach (string word in dict_words)
                {
                    string m_word = word;
                    if (word[0] == '"')
                        m_word = word.Substring(1, word.Length - 2);
                    if (m_word.Length < 2)
                        continue;
                    Word w = new Word();
                    w.word = m_word;
                    w.type = "";
                    w.definition = "";
                    words[w.word] = w;
                    if (wordLengths.ContainsKey(w.word.Length))
                        wordLengths[w.word.Length]++;
                    else
                        wordLengths[w.word.Length] = 1;
                }
            }
            else
            {
                foreach (string word in dict_words)
                {
                    string[] content = word.Split("~");
                    if (content[0].Length < 2)
                        continue;
                    Word w = new Word();
                    w.word = content[0];
                    w.type = content[1];
                    w.definition = content[2];
                    words[w.word] = w;
                    if (wordLengths.ContainsKey(w.word.Length))
                        wordLengths[w.word.Length]++;
                    else
                        wordLengths[w.word.Length] = 1;
                }
            }

            foreach (Word word in words.Values)
            {
                Node currentNode = root;
                for (int i = 0; i < word.word.Length; i++)
                {
                    int currentLetter = word.word[i] - asciiOffset;
                    if (currentNode.children[currentLetter] == null)
                    {
                        Node newNode = new Node();
                        newNode.letter = (char)(currentLetter + asciiOffset);
                        newNode.parent = currentNode;
                        newNode.word = null;
                        currentNode.children[currentLetter] = newNode;
                    }
                    currentNode = currentNode.children[currentLetter];
                    if (i == word.word.Length - 1)
                    {
                        currentNode.word = word;
                    }
                }
            }
        }
    }

    public partial class MainWindow : Window
    {
        BabTrainer b;
        TextBox[] bagBoxes;
        TextBox[,] tileBoxes = new TextBox[0,0];
        int score = 0;
        string gameCode;
        int gamesChecked = 0;
        bool canceled = false;
        bool finishedCancel = false;
        int gameCount = 0;

        ObservableCollection<string> seedList = new ObservableCollection<string>();
        List<(string, string)> seedNames = new List<(string, string)>();

        public static RoutedCommand CancelCommand = new RoutedCommand();
        public MainWindow()
        {
            CancelCommand.InputGestures.Add(new KeyGesture(Key.Escape));
            InitializeComponent();
            b = new BabTrainer(int.Parse(LetterCount.Text));
            DefinitionChooser.IsEnabled = !BabTrainer.usingWordList;
            DefinitionChooser.IsChecked = !BabTrainer.usingWordList;
			
            LoadSeedList();
            bagBoxes = new TextBox[] { Letter0, Letter1, Letter2, Letter3, Letter4, Letter5, Letter6, Letter7, Letter8, Letter9 };
            BabTrainer.lineCount = int.Parse(LineCount.Text);
            BabTrainer.lineLength = 2 * int.Parse(LetterCount.Text) + (int.Parse(RestrictionCountMin.Text) + int.Parse(RestrictionCountMax.Text)) / 2;
            
            SeedList.ItemsSource = seedList;
            SeedList.SelectionChanged += SeedSelectionChanged;
            MinWordLengthCount.TextChanged += UpdatePlurals;
            MinWordLengthAccumCount.TextChanged += UpdatePlurals;
            MinSeparateUse.TextChanged += UpdatePlurals;
            MinFixedUse.TextChanged += UpdatePlurals;
            ThreadCount.TextChanged += UpdatePlurals;
            SeedNameBox.TextChanged += UpdateSeedName;
            CapsChooser.Checked += (o, e) => SetCaps(true);
            CapsChooser.Unchecked += (o, e) => SetCaps(false);
            TilesChooser.Checked += (o, e) => { RestrictionInfo.Visibility = Visibility.Hidden; TileGrid.Visibility = Visibility.Visible; };
            TilesChooser.Unchecked += (o, e) => { RestrictionInfo.Visibility = Visibility.Visible; TileGrid.Visibility = Visibility.Hidden; };
            if((bool)TilesChooser.IsChecked)
            {
                RestrictionInfo.Visibility = Visibility.Hidden;
                TileGrid.Visibility = Visibility.Visible;
            }
            else
            {
                RestrictionInfo.Visibility = Visibility.Visible;
                TileGrid.Visibility = Visibility.Hidden;
            }
                
            this.Closed += OnExit;

            MinWordLengthCount.GotKeyboardFocus += HandleFocus;
            MinWordLengthCategory.GotKeyboardFocus += HandleFocus;
            MinWordLengthAccumCount.GotKeyboardFocus += HandleFocus;
            MinWordLengthAccumCategory.GotKeyboardFocus += HandleFocus;
            MinFixedUse.GotKeyboardFocus += HandleFocus;
            MinSeparateUse.GotKeyboardFocus += HandleFocus;
            RestrictionCountMin.GotKeyboardFocus += HandleFocus;
            RestrictionCountMax.GotKeyboardFocus += HandleFocus;
            LetterCount.GotKeyboardFocus += HandleFocus;
            LineCount.GotKeyboardFocus += HandleFocus;
            SeedNameBox.GotKeyboardFocus += HandleFocus;
            ThreadCount.GotKeyboardFocus += HandleFocus;
            HintMinLength.GotKeyboardFocus += HandleFocus;
            LineLengthFactor.GotKeyboardFocus += HandleFocus;

            MinWordLengthCount.LostKeyboardFocus += HandleUnfocus;
            MinWordLengthCategory.LostKeyboardFocus += HandleUnfocus;
            MinWordLengthAccumCount.LostKeyboardFocus += HandleUnfocus;
            MinWordLengthAccumCategory.LostKeyboardFocus += HandleUnfocus;
            MinFixedUse.LostKeyboardFocus += HandleUnfocus;
            MinSeparateUse.LostKeyboardFocus += HandleUnfocus;
            RestrictionCountMin.LostKeyboardFocus += HandleUnfocus;
            RestrictionCountMax.LostKeyboardFocus += HandleUnfocus;
            LetterCount.LostKeyboardFocus += HandleUnfocus;
            LineCount.LostKeyboardFocus += HandleUnfocus;
            SeedNameBox.LostKeyboardFocus += HandleUnfocus;
            ThreadCount.LostKeyboardFocus += HandleUnfocus;
            HintMinLength.LostKeyboardFocus += HandleUnfocus;
            LineLengthFactor.LostKeyboardFocus += HandleUnfocus;

            ThreadCount.Text = (Environment.ProcessorCount - 2).ToString();
            HideInfoChooser.Checked += (o, e) => InfoBox.Visibility = ScorePercent.Visibility = Visibility.Hidden;
            HideInfoChooser.Unchecked += (o, e) => InfoBox.Visibility = ScorePercent.Visibility = Visibility.Visible;
            UpdatePlurals(null, null);
            SetCaps((bool)CapsChooser.IsChecked);
            BuildTiles(); // build tiles after to set caps
            NewGame(null, null);
        }

        public void BuildTiles()
        {
            TileTemplate.Visibility = Visibility.Hidden;
            TileGrid.Rows = BabTrainer.lineCount;
            TileGrid.Columns = BabTrainer.lineLength;
            TileGrid.Children.RemoveRange(0, TileGrid.Children.Count);
            TileGrid.Width = TileGrid.Columns * TileTemplate.Width;
            TileGrid.Height = TileGrid.Rows * TileTemplate.Height + (TileGrid.Rows - 1) * 14;
            tileBoxes = new TextBox[TileGrid.Rows, TileGrid.Columns];
            for (int i = 0; i < TileGrid.Rows; i++)
            {
                for (int j = 0; j < TileGrid.Columns; j++)
                {
                    TextBox t = (TextBox)XamlReader.Parse(XamlWriter.Save(TileTemplate));
                    t.Visibility = Visibility.Visible;
                    TileGrid.Children.Add(t);
                    tileBoxes[i, j] = t;
                }
            }
            if((bool)CapsChooser.IsChecked)
            {
                foreach (var box in tileBoxes)
                {
                    box.Text = box.Text.ToUpper();
                    box.TextChanged += Upperize;
                }
            }
            else
            {
                foreach (var box in tileBoxes)
                {
                    box.Text = box.Text.ToLower();
                }
            }
        }

        public void SetCaps(bool caps)
        {
            if (caps)
            {
                foreach (TextBox box in bagBoxes)
                {
                    box.Text = box.Text.ToUpper();
                    box.TextChanged += Upperize;
                }
                foreach (var box in tileBoxes)
                {
                    box.Text = box.Text.ToUpper();
                    box.TextChanged += Upperize;
                }
                RestrictionInfo.Text = RestrictionInfo.Text.ToUpper();
                RestrictionInfo.TextChanged += Upperize;
                InputBox.Text = InputBox.Text.ToUpper();
                InputBox.TextChanged += Upperize;
            }
            else
            {
                foreach (TextBox box in bagBoxes)
                {
                    box.TextChanged -= Upperize;
                    box.Text = box.Text.ToLower();
                }
                foreach (var box in tileBoxes)
                {
                    box.TextChanged -= Upperize;
                    box.Text = box.Text.ToLower();
                }
                RestrictionInfo.TextChanged -= Upperize;
                RestrictionInfo.Text = RestrictionInfo.Text.ToLower();
                InputBox.Text = InputBox.Text.ToLower();
                InputBox.TextChanged -= Upperize;
            }
        }

        public void Upperize(object o, TextChangedEventArgs e)
        {
            ((TextBox)o).TextChanged -= Upperize;
            int index = ((TextBox)o).CaretIndex;
            ((TextBox)o).Text = ((TextBox)o).Text.ToUpper();
            ((TextBox)o).CaretIndex = index;
            ((TextBox)o).TextChanged += Upperize;
        }

        public void OnExit(object o, EventArgs e)
        {
            SaveSeedList();
        }

        public bool stopSyncingSeedName = false;
        string focusStore = "";

        public void HandleFocus(object o, KeyboardFocusChangedEventArgs args)
        {
            if ((TextBox)o == SeedNameBox)
            {
                if (lastSelectedSeed != -1 && SeedNameBox.Text == seedNames[lastSelectedSeed].Item1)
                {
                    stopSyncingSeedName = true;
                    focusStore = ((TextBox)o).Text;
                    ((TextBox)o).Text = "";
                }
                return;
            }
            else
            {
                focusStore = ((TextBox)o).Text;
                ((TextBox)o).Text = "";
            }
        }

        public void HandleUnfocus(object o, KeyboardFocusChangedEventArgs args)
        {
            if ((TextBox)o == SeedNameBox)
                stopSyncingSeedName = false;
            if (((TextBox)o).Text == "")
                ((TextBox)o).Text = focusStore;
            focusStore = "";
            if ((TextBox)o == SeedNameBox)
                SaveSeedList();
        }

        public void UpdatePlurals(object sender, TextChangedEventArgs args)
        {
            MinWordLengthDescriptor.Text = "letter words";
            MinWordLengthAccumDescriptor.Text = "+letter words";
            MinFixedUseDescriptor.Text = "fixed letters";
            MinSeparateUseDescriptor.Text = "tiles";
            ThreadCountDescriptor.Text = "threads";

            if (int.TryParse(MinWordLengthCount.Text, out int mwlc) && mwlc == 1)
                MinWordLengthDescriptor.Text = MinWordLengthDescriptor.Text.Substring(0, MinWordLengthDescriptor.Text.Length - 1);

            if (int.TryParse(MinWordLengthAccumCount.Text, out int mwla) && mwla == 1)
                MinWordLengthAccumDescriptor.Text = MinWordLengthAccumDescriptor.Text.Substring(0, MinWordLengthAccumDescriptor.Text.Length - 1);

            if (int.TryParse(MinFixedUse.Text, out int mfu) && mfu == 1)
                MinFixedUseDescriptor.Text = MinFixedUseDescriptor.Text.Substring(0, MinFixedUseDescriptor.Text.Length - 1);

            if (int.TryParse(MinSeparateUse.Text, out int msu) && msu == 1)
                MinSeparateUseDescriptor.Text = MinSeparateUseDescriptor.Text.Substring(0, MinSeparateUseDescriptor.Text.Length - 1);

            if (int.TryParse(ThreadCount.Text, out int tc) && tc == 1)
                ThreadCountDescriptor.Text = ThreadCountDescriptor.Text.Substring(0, ThreadCountDescriptor.Text.Length - 1);
        }

        int lastSelectedSeed = -1;

        public void SeedSelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if (SeedList.SelectedItem == null)
            {
                if (seedList.Count == 0)
                    lastSelectedSeed = -1;
                return;
            }
            lastSelectedSeed = SeedList.SelectedIndex;
            SeedInfo.Text = seedNames[SeedList.SelectedIndex].Item1;
            SeedGame(sender, args);
            if (!stopSyncingSeedName)
                SeedNameBox.Text = SeedList.SelectedItem.ToString();
        }

        public void UpdateSeedName(object sender, TextChangedEventArgs args)
        {
            ((TextBox)sender).TextChanged -= UpdateSeedName;
            SeedList.SelectionChanged -= SeedSelectionChanged;
            if (lastSelectedSeed == -1)
            {
                SeedNameBox.Text = "";
                return;
            }
            if (SeedNameBox.Text == "")
                seedNames[lastSelectedSeed] = (seedNames[lastSelectedSeed].Item1, seedNames[lastSelectedSeed].Item1);
            else
                seedNames[lastSelectedSeed] = (seedNames[lastSelectedSeed].Item1, SeedNameBox.Text);
            seedList[lastSelectedSeed] = seedNames[lastSelectedSeed].Item2;
            SeedList.SelectedIndex = lastSelectedSeed;
            ((TextBox)sender).TextChanged += UpdateSeedName;
            SeedList.SelectionChanged += SeedSelectionChanged;
        }

        public void LoadSeedList()
        {
            if (!File.Exists("seedlist.txt"))
                return;
            seedList.Clear();
            seedNames.Clear();
            string[] f = File.ReadAllLines("seedlist.txt");
            foreach (string l in f)
            {
                string[] c = l.Split(',');
                seedNames.Add((c[0], c[1]));
                seedList.Add(c[1]);
            }
        }

        public void SaveSeedList()
        {
            if (seedList.Count == 0 && File.Exists("seedlist.txt"))
                File.Delete("seedlist.txt");
            else
            {
                List<string> lines = new List<string>();
                foreach (var tuple in seedNames)
                    lines.Add(tuple.Item1 + "," + tuple.Item2);
                File.WriteAllLines("seedlist.txt", lines);
            }
        }

        public void OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.D1)
            {
                Hint(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.D2)
            {
                HintSameWord(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.D3)
            {
                Shuffle(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.D4)
            {
                NewGame(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Return || e.Key == Key.Space)
            {
                string enteredWord = InputBox.Text.ToLower();
                DictLoadInfo.Text = "";
                if (!b.foundWords.Contains(enteredWord) && b.remainingWords.Contains(enteredWord))
                {
                    b.foundWords.Add(enteredWord);
                    b.remainingWords.Remove(enteredWord);
                    b.remainingWordsByLength[enteredWord.Length].Remove(enteredWord);
                    score++;

                    InfoBox.Inlines.Clear();
                    foreach (var i in b.BuildRemainingText())
                        InfoBox.Inlines.Add(i);
                }
                else if (b.foundWords.Contains(enteredWord))
                {
                    BabTrainer.Word w = BabTrainer.words[enteredWord];
                    DictLoadInfo.Text = "Valid but already played: " + w.word;
                    if (!BabTrainer.usingWordList)
                        DictLoadInfo.Text += " - " + w.type + " - " + BabTrainer.HandleWordVariant(w.definition, false);
                }
                else if (BabTrainer.words.ContainsKey(enteredWord))
                {
                    BabTrainer.Word w = BabTrainer.words[enteredWord];
                    DictLoadInfo.Text = "Valid but cannot be played: " + w.word;
                    if (!BabTrainer.usingWordList)
                        DictLoadInfo.Text += " - " + w.type + " - " + BabTrainer.HandleWordVariant(w.definition, false);
                }
                else
                {
                    DictLoadInfo.Text = "Invalid word: \"" + enteredWord + "\"";
                }
                InputBox.Text = "";
                Score.Text = score.ToString();
                int percent = (int)(((float)score / (float)b.validWords.Count) * 100f);
                ScorePercent.Text = percent + "%";
                var inlines = b.BuildFoundInfo();
                FoundBox.Inlines.Clear();
                foreach (var i in inlines)
                    FoundBox.Inlines.Add(i);

                e.Handled = true;
            }
        }

        public static bool ContainsAllLetters(string s, string letters)
        {
            int hitCount = 0;
            int hitMask = 0;
            for(int j = 0; j < letters.Length; j++)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    if (s[i] == letters[j] && (1 & (hitMask >> i)) != 1)
                    {
                        hitMask = (int)(hitMask | (1 << i));
                        hitCount++;
                        break;
                    }
                }
            }
            return hitCount >= letters.Length;
        }

        DateTime lastUpdate;

        public void GameSearchTask(BabTrainer reference)
        {
            lastUpdate = DateTime.UnixEpoch;
            int constraintCounter = 0;

        newgame:
            if (canceled || finishedCancel)
                return;

            reference.NewGame(Environment.TickCount + Guid.NewGuid().GetHashCode() + gamesChecked + Thread.CurrentThread.ManagedThreadId.GetHashCode());
            int accum = 0;
            foreach (var pair in reference.remainingWordsByLength)
            {
                if (pair.Key >= minWordLengthAccumCat)
                    accum += pair.Value.Count;
            }
            bool satisfied = reference.remainingWordsByLength.ContainsKey(minWordLengthCat) && reference.remainingWordsByLength[minWordLengthCat].Count >= minWordLengthCount && accum >= minWordLengthAccumCount;
            if (satisfied)
            {
                foreach (var constraint in wordConstraints)
                {
                    constraintCounter = 0;
                    foreach (var word in reference.validWords)
                    {
                        if (word.Contains(constraint.Key))
                            constraintCounter++;
                        if (constraintCounter >= constraint.Value)
                            break;
                    }
                    if (constraintCounter >= constraint.Value)
                        continue;
                    else
                    {
                        satisfied = false;
                        break;
                    }
                }
            }
            if (satisfied)
            {
                foreach (var constraint in compositionConstraints)
                {
                    constraintCounter = 0;
                    foreach (var word in reference.validWords)
                    {
                        if (ContainsAllLetters(word, constraint.Key))
                            constraintCounter++;
                        if (constraintCounter >= constraint.Value)
                            break;
                    }
                    if (constraintCounter >= constraint.Value)
                        continue;
                    else
                    {
                        satisfied = false;
                        break;
                    }
                }
            }
            if (!satisfied)
            {
                gamesChecked++;
                if ((DateTime.Now - lastUpdate).Milliseconds > 500)
                {
                    Dispatcher.Invoke(() => { DictLoadInfo.Text = gamesChecked.ToString() + " games checked."; });
                    lastUpdate = DateTime.Now;
                }
                goto newgame;
            }
        }

        int uiBagSize;
        int minWordLengthCat;
        int minWordLengthCount;
        int minWordLengthAccumCat;
        int minWordLengthAccumCount;
        public static Dictionary<string, int> wordConstraints;
        public static Dictionary<string, int> compositionConstraints;

        public async void NewGame(object sender, RoutedEventArgs e)
        {
            InfoBox.Visibility = ScorePercent.Visibility = (bool)HideInfoChooser.IsChecked ? Visibility.Hidden : Visibility.Visible;
			
			BabTrainer.restrictionCountMin = int.Parse(RestrictionCountMin.Text);
            BabTrainer.restrictionCountMax = int.Parse(RestrictionCountMax.Text);
			
            if (BabTrainer.restrictionCountMin > BabTrainer.restrictionCountMax)
            {
                RestrictionCountMin.Text = RestrictionCountMax.Text;
                BabTrainer.restrictionCountMin = BabTrainer.restrictionCountMax;
            }

            BabTrainer.lineLengthFactor = int.Parse(LineLengthFactor.Text);
            BabTrainer.lineCount = int.Parse(LineCount.Text);
            BabTrainer.minFixed = int.Parse(MinFixedUse.Text);
            BabTrainer.minBridgeLength = int.Parse(MinSeparateUse.Text);
            uiBagSize = Math.Min(10, int.Parse(LetterCount.Text));
            minWordLengthCat = int.Parse(MinWordLengthCategory.Text);
            minWordLengthCount = int.Parse(MinWordLengthCount.Text);
            minWordLengthAccumCat = int.Parse(MinWordLengthAccumCategory.Text);
            minWordLengthAccumCount = int.Parse(MinWordLengthAccumCount.Text);
            wordConstraints = new Dictionary<string, int>();
            compositionConstraints = new Dictionary<string, int>();

            if (ConstraintLetter1.Text != "" && ConstraintCount1.Text != "")
                wordConstraints[ConstraintLetter1.Text] = int.Parse(ConstraintCount1.Text);
            if (CompositionLetter1.Text != "" && CompositionCount1.Text != "")
                compositionConstraints[CompositionLetter1.Text] = int.Parse(CompositionCount1.Text);
            if (ConstraintLetter2.Text != "" && ConstraintCount2.Text != "")
                wordConstraints[ConstraintLetter2.Text] = int.Parse(ConstraintCount2.Text);
            if (CompositionLetter2.Text != "" && CompositionCount2.Text != "")
                compositionConstraints[CompositionLetter2.Text] = int.Parse(CompositionCount2.Text);
            HashSet<char> s = new HashSet<char>();
            foreach (var c in wordConstraints)
                s.UnionWith(c.Key);
            foreach (var c in compositionConstraints)
                s.UnionWith(c.Key);

            BabTrainer.requiredLetters = new int[s.Count];
            string lstr = "";
            int k = 0;
            foreach (var c in s)
            {
                BabTrainer.requiredLetters[k] = (int)(c - BabTrainer.asciiOffset);
                lstr += c;
                k++;
            }

            gamesChecked = 0;
            canceled = false;
            finishedCancel = false;
           
            int threadCount = 1;
            int.TryParse(ThreadCount.Text, out threadCount);
            threadCount = Math.Max(1, threadCount);
            BabTrainer[] references = new BabTrainer[threadCount];
            Task[] tasks = new Task[threadCount];
            int c1, c2;
            ThreadPool.GetMinThreads(out c1, out c2);
            ThreadPool.SetMinThreads(Math.Min(Environment.ProcessorCount, threadCount), c2);

            for (int i = 0; i < threadCount; i++)
            {
                BabTrainer rfc = references[i] = new BabTrainer(uiBagSize);
                tasks[i] = Task.Run(() => GameSearchTask(rfc));
            }

            await Task.Run(() =>
            {
                int finished = Task.WaitAny(tasks);
                finishedCancel = true;
                Task.WaitAll(tasks);
                b = references[finished];
            });
            
            if (canceled)
            {
                DictLoadInfo.Text = gamesChecked.ToString() + " games checked. Search canceled.";
                return;
            }
            else
            {
                gameCount++;
                if (gameCount != 1 || !BabTrainer.usingWordList)
                    DictLoadInfo.Text = gamesChecked.ToString() + " games checked. Game found.";
                else
                    DictLoadInfo.Text = BabTrainer.dictLoadInfo;
            }

            SeedInfo.Text = lstr + BabTrainer.lineCount.ToString("D2") + b.bagSize.ToString("D2")
                + BabTrainer.restrictionCountMin.ToString("D2") + BabTrainer.restrictionCountMax.ToString("D2")
                + BabTrainer.minFixed.ToString("D1") + BabTrainer.minBridgeLength.ToString("D1") + BabTrainer.lineLengthFactor.ToString("D2") + ((uint)b.seed).ToString();

            gameCode = SeedInfo.Text;
            for (int i = 0; i < b.bagSize; i++)
            {
                bagBoxes[i].Text = ((char)(b.bag[i] + BabTrainer.asciiOffset)).ToString();
            }
            for (int i = b.bagSize; i < bagBoxes.Length; i++)
            {
                bagBoxes[i].Text = "";
            }
            ResetUI();
        }

        public void CancelSearch(object sender, RoutedEventArgs e)
        {
            canceled = true;
        }

        public void ResetCurrentGame(object sender, RoutedEventArgs e)
        {
            DoSeedGame(gameCode);
        }

        public void SeedGame(object sender, RoutedEventArgs e)
        {
            gameCode = SeedInfo.Text;
            DoSeedGame(gameCode);
        }

        public void SaveSeed(object sender, RoutedEventArgs e)
        {
            foreach (var tuple in seedNames)
            {
                if (tuple.Item1 == SeedInfo.Text)
                    return;
            }
            seedList.Insert(0, SeedInfo.Text);
            seedNames.Insert(0, (SeedInfo.Text, SeedInfo.Text));
            lastSelectedSeed = 0;
            SeedNameBox.TextChanged -= UpdateSeedName;
            SeedNameBox.Text = SeedInfo.Text;
            SeedNameBox.TextChanged += UpdateSeedName;
            SaveSeedList();
        }

        public void DeleteSeed(object sender, RoutedEventArgs e)
        {
            if (SeedList.SelectedIndex == -1 || seedList.Count == 0)
                return;
            SeedList.SelectionChanged -= SeedSelectionChanged;
            int index = SeedList.SelectedIndex;
            seedList.RemoveAt(index);
            seedNames.RemoveAt(index);
            if (index < seedList.Count)
                SeedList.SelectedIndex = index;
            else if (index == seedList.Count)
                SeedList.SelectedIndex = seedList.Count - 1;
            lastSelectedSeed = SeedList.SelectedIndex;
            if (lastSelectedSeed != -1)
                SeedNameBox.Text = SeedList.SelectedItem.ToString();
            else
                SeedNameBox.Text = "";
            SaveSeedList();
            SeedList.SelectionChanged += SeedSelectionChanged;
        }

        public void DoSeedGame(string code)
        {
            string lstr = "";
            for (int i = 0; i < code.Length; i++)
                if (code[i] - BabTrainer.asciiOffset >= 0)
                    lstr += code[i];
            BabTrainer.requiredLetters = new int[lstr.Length];
            for (int i = 0; i < lstr.Length; i++)
                BabTrainer.requiredLetters[i] = (int)(lstr[i] - BabTrainer.asciiOffset);

            string rest = code.Substring(lstr.Length);

            LineCount.Text = rest.Substring(0, 2).TrimStart('0');
            LetterCount.Text = rest.Substring(2, 2).TrimStart('0');
            RestrictionCountMin.Text = rest.Substring(4, 2).TrimStart('0');
            RestrictionCountMax.Text = rest.Substring(6, 2).TrimStart('0');
            MinFixedUse.Text = rest.Substring(8, 1);
            MinSeparateUse.Text = rest.Substring(9, 1);
            LineLengthFactor.Text = rest.Substring(10, 2).TrimStart('0');

            ConstraintLetter1.Text = "";
            ConstraintLetter2.Text = "";
            CompositionLetter1.Text = "";
            CompositionLetter2.Text = "";
            CompositionCount1.Text = "";
            CompositionCount2.Text = "";
            ConstraintCount1.Text = "";
            ConstraintCount2.Text = "";

            if (lstr != "")
            {
                CompositionLetter1.Text = lstr;
                CompositionCount1.Text = "1";
            }

            BabTrainer.lineCount = int.Parse(LineCount.Text);
            BabTrainer.minFixed = int.Parse(MinFixedUse.Text);
            BabTrainer.minBridgeLength = int.Parse(MinSeparateUse.Text);
            BabTrainer.restrictionCountMin = int.Parse(RestrictionCountMin.Text);
            BabTrainer.restrictionCountMax = int.Parse(RestrictionCountMax.Text);
            BabTrainer.lineLengthFactor = int.Parse(LineLengthFactor.Text);

            b = new BabTrainer(int.Parse(LetterCount.Text));

            b.NewGame((int)uint.Parse(rest.Substring(12)));

            for (int i = 0; i < b.bagSize; i++)
            {
                bagBoxes[i].Text = ((char)(b.bag[i] + BabTrainer.asciiOffset)).ToString();
            }
            for (int i = b.bagSize; i < bagBoxes.Length; i++)
            {
                bagBoxes[i].Text = "";
            }
            ResetUI();
        }

        void ResetUI()
        {
            score = 0;
            Score.Text = score.ToString();
            ScorePercent.Text = "0%";
            b.BuildRestrictionText();
            RestrictionInfo.Text = b.restrictionText.ToString();
            if (BabTrainer.lineCount != tileBoxes.GetLength(0) || BabTrainer.lineLength != tileBoxes.GetLength(1))
                BuildTiles();
            for (int i = 0; i < tileBoxes.GetLength(0); i++)
                for (int j = 0; j < tileBoxes.GetLength(1); j++)
                    tileBoxes[i, j].Text = "";
            for (int i = 0; i < b.restrictionSets.Length; i++)
                for(int j = 0; j < b.restrictionSets[i].Length; j++)
                    tileBoxes[i, b.restrictionSets[i][j].Item1].Text = ((char)(b.restrictionSets[i][j].Item2 + BabTrainer.asciiOffset)).ToString();
            InfoBox.Text = "";
            InfoBox.Inlines.Clear();
            foreach (var i in b.BuildRemainingText())
                InfoBox.Inlines.Add(i);
            HintBox.Text = "";
            FoundBox.Text = "";
            FoundBox.Inlines.Clear();
        }

        BabTrainer.Word currentHintWord;
        int currentHintPos;

        public void Hint(object sender, RoutedEventArgs e)
        {
            currentHintWord = b.GetRandomRemaining(int.Parse(HintMinLength.Text));
            if (currentHintWord == null)
                HintBox.Text = "No remaining words of that minimum length.";
            else if ((bool)DefinitionChooser.IsChecked && !BabTrainer.usingWordList)
                HintBox.Text = currentHintWord.type + " - " + BabTrainer.HandleWordVariant(currentHintWord.definition, true);
            else
                HintSameWord(sender, e);
        }

        public void HintSameWord(object sender, RoutedEventArgs e)
        {
            if (currentHintWord == null)
                return;
            var r = new Random();
        random:
            int nextHintPos = r.Next(0, currentHintWord.word.Length);
            if (nextHintPos == currentHintPos)
                goto random;
            currentHintPos = nextHintPos;
            string hint = String.Concat(Enumerable.Repeat("_ ", currentHintPos)) + currentHintWord.word[currentHintPos] + String.Concat(Enumerable.Repeat(" _", Math.Max(0, currentHintWord.word.Length - currentHintPos - 1)));
            if ((bool)DefinitionChooser.IsChecked && !BabTrainer.usingWordList)
                hint += "\n" + currentHintWord.type + " - " + BabTrainer.HandleWordVariant(currentHintWord.definition, true);
                
            HintBox.Text = hint;
        }

        public void OnResults(object sender, RoutedEventArgs e)
        {
            InfoBox.Visibility = ScorePercent.Visibility = Visibility.Visible;
            string[] constraintsA = new string[] { ConstraintLetter1.Text, ConstraintLetter2.Text };
            string[] constraintsB = new string[] { CompositionLetter1.Text, CompositionLetter2.Text };
            var inlines = b.BuildResultText(constraintsA, constraintsB);
            InfoBox.Inlines.Clear();
            foreach (var i in inlines)
                InfoBox.Inlines.Add(i);
        }

        public void Shuffle(object sender, RoutedEventArgs e)
        {
            List<int> bag = new List<int>(b.bag);
            int[] newLetters = new int[b.bagSize];
            var r = new Random();
            for (int i = 0; i < b.bagSize; i++)
            {
                int index = r.Next(0, bag.Count);
                newLetters[i] = bag[index];
                bag.RemoveAt(index);
            }
				
            for (int i = 0; i < b.bagSize; i++)
            {
                bagBoxes[i].Text = ((char)(newLetters[i] + BabTrainer.asciiOffset)).ToString();
            }
            for (int i = b.bagSize; i < bagBoxes.Length; i++)
            {
                bagBoxes[i].Text = "";
            }
        }
    }
}
