namespace NewickParser;

/// <summary>
/// Translated from: https://gist.github.com/Ad115/34dfc6560b64779a40c1a929f560511b 
/// </summary>
public static class NewickTreeParser
{
    /// <summary>
    /// Parse recursively the newick representation of a complete newick tree.  
    /// Root and child nodes are assembled using the aggregator function. 
    ///  
    /// The distance parser and the feature parser allow to modify the parsing of 
    /// the raw strings.
    /// </summary>
    public static T ParseNewick<T>(
        string newick,
        Aggregator<T> aggregator,
        Func<string, float?>? distanceParser = null,
        Func<string, string>? featureParser = null)
    {
        if (!newick.EndsWith(';'))
        {
            throw new ArgumentException("Tree in Newick format must end with ';'");
        }

        string root = newick[..^1];
        return ParseNewickSubtree(root, aggregator, distanceParser, featureParser);
    }

    /// <summary>
    /// Find the index of the first closing parenthesis that is not matched to an 
    /// opening one starting from the given `start` position. 
    /// 
    /// Example: '((),())()' -> 6
    ///                 ^
    /// 
    /// The `pair` argument allows to specify diferent opening/closing types
    /// of parenthesis.
    /// 
    /// Example:
    ///     _find_closing_brackets = partial(_find_closing, pair='[]') 
    /// 
    /// </summary>
    static int FindClosing(string str, int start = 1, Tuple<char, char>? pair = null)
    {
        pair ??= Tuple.Create('(', ')');
        char opening = pair.Item1;
        char closing = pair.Item2;

        int nextClosing = str.IndexOf(closing, start);
        int nextOpening = str.IndexOf(opening, start);

        if (nextOpening == -1 || nextClosing < nextOpening)
        {
            return nextClosing;
        }

        int skip = FindClosing(str, nextOpening + 1, pair);
        return FindClosing(str, skip + 1, pair);
    }

    /// <summary>
    /// A subtree consists of:
    ///     children, label, branch length/support, comments/features
    /// 
    /// Example:
    ///     '(A,B)root:10.0[x=xx]' -> ['(A,B)', 'root', '10.0', 'x=xx']
    /// </summary>
    static Tuple<string, string, string, string> PartsOfSubtree(string newick)
    {
        string children = "";
        string rest = newick;

        if (newick.StartsWith('('))
        {
            // Node has children
            int childrenEnd = FindClosing(newick);
            children = newick.Substring(1, childrenEnd - 1);
            rest = newick[(childrenEnd + 1)..];
        }

        string comment = "";
        int commentStart = rest.IndexOf('[');
        if (commentStart != -1)
        {
            // Extract comments from the end
            comment = rest.Substring(commentStart + 1, rest.Length - commentStart - 2); // Remove ']' from the end
            rest = rest[..commentStart];
        }

        string label = rest;
        string length = "";
        int colonIndex = rest.IndexOf(':');
        if (colonIndex != -1)
        {
            // Extract branch length
            label = rest[..colonIndex];
            length = rest[(colonIndex + 1)..];
        }

        return Tuple.Create(children, label.Trim(), length, comment);
    }

    /// <summary>
    /// From a comma-sepparated list of newick-formatted nodes, return the final
    /// position of the first one.
    /// 
    /// Examples: 
    ///     '(A:1,(C[x],D))name:1.[c], (X,Y),,[xxx]' -> 23
    ///     '(X,Y),,[xxx]' -> 5
    ///     '[xxx]' -> 4
    /// </summary>
    static int NextNodeEnd(string nodesStr)
    {
        nodesStr = nodesStr.Trim();

        int currentEnd = 0;
        // Skip children
        if (nodesStr.StartsWith('('))
        {
            currentEnd = FindClosing(nodesStr);
        }

        // Skip label, distances and comments
        // Strategy: find the next comma that is not surrounded by brackets or
        //    parentheses or the end of the string
        while (currentEnd < nodesStr.Length)
        {
            char charAtIndex = nodesStr[currentEnd];

            switch (charAtIndex)
            {
                case '(' or '[':
                    currentEnd = FindClosing(nodesStr, currentEnd + 1,
                        Tuple.Create(charAtIndex, charAtIndex == '(' ? ')' : ']'));
                    continue;
                case ',':
                    // Ah, the lonely comma...
                    return currentEnd - 1;
                default:
                    currentEnd++;
                    break;
            }
        }

        return nodesStr.Length - 1;
    }

    /// <summary>
    /// Separate the nodes from a comma-sepparated list.
    /// 
    /// Example:  '(a,b), , :12, c[xxx]' -> ['(a,b)', '', ':12', 'c[xxx]']
    /// </summary>
    static List<string> SplitNodes(string nodesStr)
    {
        nodesStr = nodesStr.Trim();
        List<string> nodes = [];

        while (!string.IsNullOrEmpty(nodesStr))
        {
            int nextNodeEnd = NextNodeEnd(nodesStr);

            string node = nodesStr[..(nextNodeEnd + 1)];

            nodes.Add(node);

            nodesStr = nodesStr[(nextNodeEnd + 1)..].TrimStart(',');
            nodesStr = nodesStr.Trim();
        }

        return nodes;
    }

    static float? SimpleDistance(string dist)
    {
        return string.IsNullOrEmpty(dist) ? (float?)null : float.Parse(dist);
    }

    static string SimpleFeature(string feat)
    {
        return feat;
    }

    public delegate T Aggregator<T>(string label, List<T> children, float? distance, string features);

    static T ParseNewickSubtree<T>(
        string newick,
        Aggregator<T> aggregator,
        Func<string, float?>? distanceParser = null,
        Func<string, string>? featureParser = null)
    {
        distanceParser ??= SimpleDistance;
        featureParser ??= SimpleFeature;

        Tuple<string, string, string, string> parts = PartsOfSubtree(newick.Trim());
        List<T> children = SplitNodes(parts.Item1)
            .Select(childStr => ParseNewickSubtree(childStr, aggregator, distanceParser, featureParser)).ToList();

        string features = featureParser(parts.Item4);
        float? distance = distanceParser(parts.Item3);

        return aggregator(parts.Item2, children, distance, features);
    }
}