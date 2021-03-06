// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Completion.Providers;
using Microsoft.CodeAnalysis.Editor.Implementation.IntelliSense.Completion;
using Microsoft.CodeAnalysis.Interactive;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;

// This is in completely the wrong place.  It needs to be put in a proper place once we create a real
// interactive services assembly.

namespace Microsoft.CodeAnalysis.Editor.Implementation.Interactive
{
    // TODO(cyrusn): Use a predefined name here.
    [ExportCompletionProvider("LoadCommandCompletionProvider", InteractiveLanguageNames.InteractiveCommand)]
    internal partial class LoadCommandCompletionProvider : TextCompletionProvider
    {
        private const string NetworkPath = "\\\\";
        private static readonly Regex s_directiveRegex = new Regex(@"#load\s+(""[^""]*""?)", RegexOptions.Compiled);

        public override CompletionList GetCompletionList(SourceText text, int position, CompletionTriggerInfo triggerInfo, CancellationToken cancellationToken = default(CancellationToken))
        {
            var items = this.GetItems(text, position, triggerInfo, cancellationToken);
            if (items == null || !items.Any())
            {
                return null;
            }

            return new CompletionList(items);
        }

        public override bool IsTriggerCharacter(SourceText text, int characterPosition, OptionSet options)
        {
            return PathCompletionUtilities.IsTriggerCharacter(text, characterPosition);
        }

        private string GetPathThroughLastSlash(SourceText text, int position, Group quotedPathGroup)
        {
            return PathCompletionUtilities.GetPathThroughLastSlash(
                quotedPath: quotedPathGroup.Value,
                quotedPathStart: GetQuotedPathStart(text, position, quotedPathGroup),
                position: position);
        }

        private TextSpan GetTextChangeSpan(SourceText text, int position, Group quotedPathGroup)
        {
            return PathCompletionUtilities.GetTextChangeSpan(
                quotedPath: quotedPathGroup.Value,
                quotedPathStart: GetQuotedPathStart(text, position, quotedPathGroup),
                position: position);
        }

        private static int GetQuotedPathStart(SourceText text, int position, Group quotedPathGroup)
        {
            return text.Lines.GetLineFromPosition(position).Start + quotedPathGroup.Index;
        }

        private ImmutableArray<CompletionItem> GetItems(SourceText text, int position, CompletionTriggerInfo triggerInfo, CancellationToken cancellationToken)
        {
            var line = text.Lines.GetLineFromPosition(position);
            var lineText = text.ToString(TextSpan.FromBounds(line.Start, position));
            var match = s_directiveRegex.Match(lineText);
            if (!match.Success)
            {
                return ImmutableArray<CompletionItem>.Empty;
            }

            var quotedPathGroup = match.Groups[1];
            var quotedPath = quotedPathGroup.Value;
            var endsWithQuote = PathCompletionUtilities.EndsWithQuote(quotedPath);
            if (endsWithQuote && (position >= line.Start + match.Length))
            {
                return ImmutableArray<CompletionItem>.Empty;
            }

            var buffer = text.Container.GetTextBuffer();
            var snapshot = text.FindCorrespondingEditorTextSnapshot();
            if (snapshot == null)
            {
                return ImmutableArray<CompletionItem>.Empty;
            }

            var fileSystem = PathCompletionUtilities.GetCurrentWorkingDirectoryDiscoveryService(snapshot);

            var searchPaths = ImmutableArray.Create<string>(fileSystem.CurrentDirectory);

            var helper = new FileSystemCompletionHelper(
                this,
                GetTextChangeSpan(text, position, quotedPathGroup),
                fileSystem,
                Glyph.OpenFolder,
                Glyph.CSharpFile,
                searchPaths: searchPaths,
                allowableExtensions: new[] { ".csx" },
                itemRules: ItemRules.Instance);

            var pathThroughLastSlash = this.GetPathThroughLastSlash(text, position, quotedPathGroup);

            return helper.GetItems(pathThroughLastSlash, documentPath: null);
        }

        protected override async Task<IEnumerable<CompletionItem>> GetItemsWorkerAsync(Document document, int position, CompletionTriggerInfo triggerInfo, CancellationToken cancellationToken)
        {
            var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            return GetItems(text, position, triggerInfo, cancellationToken);
        }
    }
}
