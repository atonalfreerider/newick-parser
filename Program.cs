public class Program
{
    public static void Main(string[] args)
    {
        NewickTreeParser.Aggregator aggregator = (label, children, distance, features) => new { label, children, distance, features };
        string newick = File.ReadAllText(args[0]);
        object tree = NewickTreeParser.ParseNewick(newick, aggregator);
        Console.WriteLine(tree);
    }
}

public static class NewickTreeParser
{
    public delegate object Aggregator(string label, List<object> children, float? distance, object features);
    public delegate float? DistanceParser(string distance);
    public delegate object FeatureParser(string features);

    // Method to find the closing parenthesis or bracket matching the first character in 'input', starting from 'start'.
    static int FindClosing(string input, int start = 0, char[]? pair = null)
    {
        pair ??= ['(', ')'];

        int depth = 1; // Starts inside one level of depth, after the opening character
        for (int i = start; i < input.Length; i++)
        {
            if (input[i] == pair[0]) depth++;
            else if (input[i] == pair[1]) depth--;

            if (depth == 0) return i;
        }
        return -1; // Not found, which should not happen in well-formed input
    }

    static (string, string, string, string) ParseSubtreeParts(string newick)
    {
        string children = string.Empty, label = string.Empty, length = string.Empty, comment = string.Empty;

        if (newick.StartsWith("("))
        {
            int end = FindClosing(newick);
            children = newick.Substring(1, end - 1);
            newick = newick[(end + 1)..];
        }

        int commentStart = newick.IndexOf('[');
        if (commentStart != -1)
        {
            comment = newick.Substring(commentStart + 1, newick.Length - commentStart - 2);
            newick = newick[..commentStart];
        }

        int colonIndex = newick.IndexOf(':');
        if (colonIndex != -1)
        {
            label = newick[..colonIndex];
            length = newick[(colonIndex + 1)..];
        }
        else
        {
            label = newick;
        }

        return (children, label, length, comment);
    }

    // Method to separate nodes from a comma-separated list of newick-formatted nodes.
    static List<string> SplitNodes(string nodesStr)
    {
        nodesStr = nodesStr.Trim();
        List<string> nodes = [];

        if (string.IsNullOrEmpty(nodesStr)) return nodes;

        int currentEnd = 0;
        while (currentEnd < nodesStr.Length)
        {
            if (nodesStr[currentEnd] == '(') // Node with children
            {
                int closeIndex = FindClosing(nodesStr, currentEnd + 1, ['(', ')']);
                nodes.Add(nodesStr.Substring(currentEnd, closeIndex - currentEnd + 1));
                currentEnd = closeIndex + 1; // Move past the closing parenthesis
            }
            else // Leaf node
            {
                int nextComma = nodesStr.IndexOf(',', currentEnd);
                if (nextComma == -1) nextComma = nodesStr.Length; // If no more commas, go to end of string
                nodes.Add(nodesStr.Substring(currentEnd, nextComma - currentEnd));
                currentEnd = nextComma + 1; // Move past the comma
            }

            // Skip consecutive commas and whitespace
            while (currentEnd < nodesStr.Length && (nodesStr[currentEnd] == ',' || char.IsWhiteSpace(nodesStr[currentEnd])))
            {
                if (nodesStr[currentEnd] == ',')
                {
                    nodes.Add(""); // Add empty node for each additional comma
                }
                currentEnd++;
            }
        }

        return nodes;
    }

    static object ParseNewickSubtree(string newick, Aggregator aggregator, DistanceParser? distanceParser = null, FeatureParser? featureParser = null)
    {
        distanceParser ??= DefaultDistanceParser;
        featureParser ??= DefaultFeatureParser;

        (string? childrenStr, string? label, string? distanceStr, string? commentStr) = ParseSubtreeParts(newick.Trim());

        List<object> children = SplitNodes(childrenStr).Select(childStr => ParseNewickSubtree(childStr, aggregator, distanceParser, featureParser)).ToList();
        object features = featureParser(commentStr);
        float? distance = distanceParser(distanceStr);

        return aggregator(label, children, distance, features);
    }

    public static object ParseNewick(string newick, Aggregator aggregator, DistanceParser? distanceParser = null, FeatureParser? featureParser = null)
    {
        if (!newick.EndsWith(";")) throw new ArgumentException("Newick string must end with a semicolon (;).");
        newick = newick[..^1]; // Remove the trailing semicolon

        return ParseNewickSubtree(newick, aggregator, distanceParser, featureParser);
    }

    static float? DefaultDistanceParser(string distance)
    {
        return string.IsNullOrWhiteSpace(distance) ? (float?)null : float.Parse(distance);
    }

    static string DefaultFeatureParser(string features)
    {
        return features; // Simply returns the string, user should define their own feature parsing logic.
    }
}
