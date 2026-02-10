using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Soenneker.Utils.Directory.Abstract;

public static class GuidPatternFixer
{
    /// <summary>
    /// Scans all C# files under <paramref name="srcRoot"/>, finds any
    /// ...AsList() is List of Guid patterns and rewrites them to List of Guid?.
    /// </summary>
    public static async ValueTask FixDirectoryAsync(string srcRoot, IDirectoryUtil directoryUtil, CancellationToken cancellationToken = default)
    {
        if (!(await directoryUtil.Exists(srcRoot, cancellationToken))) return;
        List<string> paths = await directoryUtil.GetFilesByExtension(srcRoot, "cs", true, cancellationToken);
        foreach (string path in paths)
        {
            string text = await File.ReadAllTextAsync(path, cancellationToken);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(text);
            SyntaxNode root = await tree.GetRootAsync(cancellationToken);
            var rewriter = new PatternRewriter();
            SyntaxNode newRoot = rewriter.Visit(root);
            if (newRoot != null && !newRoot.IsEquivalentTo(root))
                await File.WriteAllTextAsync(path, newRoot.ToFullString(), cancellationToken);
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