// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.UnitTests.Formatting
{
    public abstract class FormattingTestBase
    {
        protected void AssertFormat(
            string expected,
            string code,
            string language,
            bool debugMode = false,
            Dictionary<OptionKey, object> changedOptionSet = null,
            bool testWithTransformation = true)
        {
            AssertFormat(expected, code, SpecializedCollections.SingletonEnumerable(new TextSpan(0, code.Length)), language, debugMode, changedOptionSet, testWithTransformation);
        }

        protected void AssertFormat(
            string expected,
            string code,
            IEnumerable<TextSpan> spans,
            string language,
            bool debugMode = false,
            Dictionary<OptionKey, object> changedOptionSet = null,
            bool treeCompare = true,
            ParseOptions parseOptions = null)
        {
            using (var workspace = new AdhocWorkspace())
            {
                var project = workspace.CurrentSolution.AddProject("Project", "Project.dll", language);
                if (parseOptions != null)
                {
                    project = project.WithParseOptions(parseOptions);
                }

                var document = project.AddDocument("Document", SourceText.From(code));

                var syntaxTree = document.GetSyntaxTreeAsync().Result;

                var options = workspace.Options;
                if (changedOptionSet != null)
                {
                    foreach (var entry in changedOptionSet)
                    {
                        options = options.WithChangedOption(entry.Key, entry.Value);
                    }
                }

                var root = syntaxTree.GetRoot();
                AssertFormat(workspace, expected, root, spans, options, document.GetTextAsync().Result);

                // format with node and transform
                AssertFormatWithTransformation(workspace, expected, root, spans, options, treeCompare, parseOptions);
            }
        }

        protected abstract SyntaxNode ParseCompilation(string text, ParseOptions parseOptions);

        protected void AssertFormatWithTransformation(
            Workspace workspace, string expected, SyntaxNode root, IEnumerable<TextSpan> spans, OptionSet optionSet, bool treeCompare = true, ParseOptions parseOptions = null)
        {
            var newRootNode = Formatter.Format(root, spans, workspace, optionSet, CancellationToken.None);

            Assert.Equal(expected, newRootNode.ToFullString());

            // test doesn't use parsing option. add one if needed later
            var newRootNodeFromString = ParseCompilation(expected, parseOptions);

            if (treeCompare)
            {
                // simple check to see whether two nodes are equivalent each other.
                Assert.True(newRootNodeFromString.IsEquivalentTo(newRootNode));
            }
        }

        protected static void AssertFormat(Workspace workspace, string expected, SyntaxNode root, IEnumerable<TextSpan> spans, OptionSet optionSet, SourceText sourceText)
        {
            var result = Formatter.GetFormattedTextChanges(root, spans, workspace, optionSet);
            AssertResult(expected, sourceText, result);
        }

        protected static void AssertResult(string expected, SourceText sourceText, IList<TextChange> result)
        {
            var actual = sourceText.WithChanges(result).ToString();
            Assert.Equal(expected, actual);
        }
    }
}
