namespace NewickParser;

public class Program
{
    public static void Main(string[] args)
    {
        string newick = File.ReadAllText(args[0]);
        TreeNode tree = NewickTreeParser.ParseNewick<TreeNode>(
            newick,
            (label, children, distance, features) =>
                new TreeNode(label, children, distance, features));

        tree.Print();
    }
}

