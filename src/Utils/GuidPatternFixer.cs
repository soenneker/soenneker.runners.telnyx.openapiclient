using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class GuidPatternFixer
{
    /// <summary>
    /// Scans all C# files under <paramref name="srcRoot"/>, finds any
    /// `...AsList() is List<Guid> guidValue` patterns and rewrites them to
    /// `...AsList() is List<Guid?> guidValue`.
    /// </summary>
    public static void FixDirectory(string srcRoot)
    {
        if (!Directory.Exists(srcRoot)) return;
        foreach (string path in Directory.GetFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(text);
            SyntaxNode root = tree.GetRoot();
            var rewriter = new PatternRewriter();
            SyntaxNode newRoot = rewriter.Visit(root);
            if (!newRoot.IsEquivalentTo(root))
                File.WriteAllText(path, newRoot.ToFullString());
        }
    }

    private class PatternRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode VisitIsPatternExpression(IsPatternExpressionSyntax node)
        {
            // match:  expr is List<Guid> varName
            if (node.Pattern is DeclarationPatternSyntax dp && dp.Type is GenericNameSyntax gns && gns.Identifier.Text == "List" &&
                gns.TypeArgumentList.Arguments.Count == 1 && gns.TypeArgumentList.Arguments[0].ToString() == "Guid")
            {
                // replace with List<Guid?>
                TypeSyntax newType = SyntaxFactory.ParseTypeName("List<Guid?>").WithTriviaFrom(gns);
                DeclarationPatternSyntax newDp = dp.WithType(newType);
                return node.WithPattern(newDp);
            }

            return base.VisitIsPatternExpression(node);
        }
    }
}