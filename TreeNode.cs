namespace NewickParser;

public class TreeNode
{
    public TreeNode(string label, List<TreeNode> children, float? distance, string features)
    {
        Label = label;
        Children = children;
        Distance = distance;
        Features = features;

        foreach (TreeNode treeNode in children)
        {
            treeNode.Parent = this;
        }
    }

    public string Label { get; }
    public List<TreeNode> Children { get; }
    public float? Distance { get; }
    public string Features { get; }
    
    public TreeNode? Parent { get; private set; }

    public float DistanceToRoot()
    {
        if (Parent == null)
        {
            return Distance ?? 0;
        }
        
        return (Distance ?? 0) + Parent.DistanceToRoot();
    }

    public float BranchDistanceToRoot()
    {
        return DistanceToRoot() - (Distance ?? 0);
    }

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


    public TreeNode? FindChild(string childName)
    {
        foreach (TreeNode child in Children)
        {
            if (child.Label == childName)
            {
                return child;
            }
            
            TreeNode? foundChild = child.FindChild(childName);
            if (foundChild != null)
            {
                return foundChild;
            }
        }

        return null;
    }
}