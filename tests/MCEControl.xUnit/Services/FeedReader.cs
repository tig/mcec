// Copyright © Kindel, LLC - http://www.kindel.com
// Published under the MIT License - Source on GitHub: https://github.com/tig/mcec

using System.Collections.Concurrent;
using System.IO;

namespace MCEControl.xUnit.Services;

/// <summary>
/// A TextReader fed line-by-line from a producer, so tests can pause a request stream at exact
/// points (a StringReader would hand a reading loop every line immediately). <see cref="ReadLine"/>
/// blocks until <see cref="Feed"/> supplies a line or <see cref="Eof"/> ends the stream.
/// </summary>
internal sealed class FeedReader : TextReader {
    private readonly BlockingCollection<string?> _lines = [];
    public void Feed(string line) => _lines.Add(line);
    public void Eof() => _lines.Add(null);
    public override string? ReadLine() => _lines.Take();
}
