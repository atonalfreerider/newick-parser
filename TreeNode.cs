namespace NewickParser;

public class TreeNode(string label, List<TreeNode> children, float? distance, string features)
{
    public string Label { get; } = label;
    public List<TreeNode> Children { get; } = children;
    public float? Distance { get; } = distance;
    public string Features { get; } = features;

    public override string ToString()
    {
        return Label;
    }

    public void Print(int depth = 0)
    {
        Console.WriteLine($"{new string(' ', depth)}{Label} {Distance} {Features}");

        foreach (TreeNode child in Children)
        {
            child.Print(depth + 1);
        }
    }
}