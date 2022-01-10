# BabbleTrainer
A practice tool for Babble Royale that uses the Babble Royale dictionary, definitions and letter distribution. You get a set of letters and a few lines of fixed letters and blanks and have to find as many words as possible that connect the fixed letters using your rack. You can specify certain letter compositions, word counts, bigrams and fragments that you want to appear in the results. Game seeds can be saved to a list so you can revisit them later and practice any new words or patterns that you've learned previously. You can get hints and reveal the full list of playable words with definitions, stratified by length. The list of played words is paired with definitions so you can quickly learn the meaning of correctly guessed hunches.

It is purely line-based, there is no grid or vertical play like in Babble Royale or Scrabble. It uses a threaded brute-force search to find a random game that satisfies all specified constraints. Most use cases are found instantly.

In order to load the Babble Royale dictionary with definitions you have to run the program from inside the Babble Royale folder (next to BabbleRoyale.exe). Alternatively you can load a list of plain words using a wordlist.txt file. Your list of seeds is saved to seedlist.txt in the same folder.

**[Download](https://github.com/seodin/BabbleTrainer/releases/download/v1/BabbleTrainer.exe)**

You need the .NET 5 Desktop Runtime: **[Download](https://dotnet.microsoft.com/en-us/download/dotnet/5.0)**

Example practice settings:
- Practice kills by connecting 2 words orthogonally (default settings): Set "Bridge at least X tiles" to 1 or more.
- Practice building off of your word orthogonally: Set "Use at least X fixed letters" to 1 and "Bridge at least X tiles" to 0.
- Practice anagrams and vocabulary in general: Set "Line Count" to 1 and "Use at least X fixed letters", "Bridge tiles" and "Fixed letters min/max" to 0.
- Practice finding words with "th": Set "Fragment" to "th" and the right column to 10 (for example).
- Practice finding words containing both 1 "x" and 2 "e"s in any position: Set "Letters" to "xee" and the right column to 10.
- Practice wide gaps and long words: Set "Letter Count" to 9 or 10 and "Line Length Factor" to a higher number like 20.
- Practice connecting to word fragments: Set "Use at least X fixed letters" to 3 or 4, "Bridge X tiles" to 0, "Fixed letters min/max" to moderately high numbers , "Line Length Factor" to a lower number like 1, number of words to find low like 20.

![Screenshot](/screenshot.png)
