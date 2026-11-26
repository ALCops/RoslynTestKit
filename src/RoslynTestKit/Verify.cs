using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Dynamics.Nav.CodeAnalysis.CodeActions;
using Microsoft.Dynamics.Nav.CodeAnalysis.Text;
using Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces;
using NUnit.Framework;
using RoslynTestKit.Utils;

namespace RoslynTestKit
{
    public static class Verify
    {
        public static void CodeAction(CodeAction codeAction, Document document, string expectedCode)
        {
            CodeAction(codeAction, document, expectedCode, FixAllHostVerification.None);
        }

        /// <summary>
        /// Verifies the result of <paramref name="codeAction"/> against <paramref name="expectedCode"/>.
        /// Unless <paramref name="hostVerification"/> is <see cref="FixAllHostVerification.None"/>, the
        /// text edits the editor host would send are replayed against the original documents first and
        /// that result must match as well; then the operations are applied to the workspace and compared.
        /// </summary>
        public static void CodeAction(CodeAction codeAction, Document document, string expectedCode, FixAllHostVerification hostVerification)
        {
            var operations = codeAction.GetOperationsAsync(CancellationToken.None).GetAwaiter().GetResult().ToList();
            if (operations.Count == 0)
            {
                throw RoslynTestKitException.NoOperationForCodeAction(codeAction);
            }

            if (hostVerification != FixAllHostVerification.None)
            {
                var hostCode = ReplayHostTextEdits(operations, document, hostVerification == FixAllHostVerification.TextEditsOnReparsedText);
                CompareWithExpected(hostCode, expectedCode, "host text edits");
            }

            var workspace = document.Project.Solution.Workspace;

            foreach (var operation in operations)
            {
                operation.Apply(workspace, CancellationToken.None);
            }
            var newDocument = workspace.CurrentSolution.GetDocument(document.Id);

            if (newDocument == null)
            {
                throw new InvalidOperationException("Resulting solution does not have the original document");
            }

            var sourceText = newDocument.GetTextAsync(CancellationToken.None).GetAwaiter().GetResult();
            var otherDocuments = newDocument.Project.Documents
                .Where(doc => doc.Id != document.Id)
                .Select(doc => (doc.Name, doc.GetTextAsync(CancellationToken.None).GetAwaiter().GetResult().ToString()));

            var actualCode = MergeDocuments(sourceText.ToString(), otherDocuments);
            CompareWithExpected(actualCode, expectedCode, hostVerification == FixAllHostVerification.None ? null : "workspace apply");
        }

        /// <summary>
        /// Rebuilds what the editor ends up with after the host runs the code action. The host never
        /// applies the operations: it keeps the solution the action was computed on as the base for every
        /// operation, diffs each changed document with <c>Document.GetTextChangesAsync</c>, converts each
        /// change to a line/character range of the original text, and sends all edits of a file in one
        /// batch that the editor applies to the unchanged buffer. Added documents are sent as full text (a
        /// document added by more than one operation throws, because the host would write that file twice);
        /// removed documents and operations other than <see cref="ApplyChangesOperation"/> are ignored.
        /// A document whose diff fails with the known defect of the SDK's syntax differ (see
        /// <see cref="IsKnownSyntaxDifferDefect"/>) is not replayed: edits collected for it from earlier
        /// operations are dropped, later operations are not diffed, and its text is taken from the changed
        /// solution of the last operation that changes it. That document therefore loses the replay's
        /// duplicate-insert and overlap detection entirely; only the workspace-apply comparison checks it.
        /// The skip is per document because keeping the earlier operations' edits would produce a false
        /// mismatch against the expected code. Every other document is still replayed. Any other diff
        /// failure throws <see cref="RoslynTestKitException.FixAllHostDiffFailed"/>.
        /// </summary>
        private static string ReplayHostTextEdits(IReadOnlyList<CodeActionOperation> operations, Document document, bool diffAgainstReparsedText)
        {
            // The snapshot the code action was computed on. The workspace's CurrentSolution is not used:
            // it does not contain the documents forked into the snapshot.
            var oldSolution = document.Project.Solution;
            var editsPerDocument = new Dictionary<DocumentId, List<TextChange>>();
            var addedDocuments = new Dictionary<DocumentId, (string Name, string Text)>();
            var unreplayedDocuments = new Dictionary<DocumentId, string>();

            foreach (var operation in operations.OfType<ApplyChangesOperation>())
            {
                var newSolution = operation.ChangedSolution;
                foreach (var projectChanges in newSolution.GetChanges(oldSolution).GetProjectChanges())
                {
                    foreach (var addedId in projectChanges.GetAddedDocuments())
                    {
                        var added = newSolution.GetDocument(addedId)!;
                        if (addedDocuments.ContainsKey(addedId))
                        {
                            // Every operation is diffed against the same original snapshot, so the host sends
                            // one full-text edit per operation that adds the document: two writes to one new file.
                            throw RoslynTestKitException.FixAllHostDocumentAddedTwice(added.Name);
                        }

                        addedDocuments.Add(addedId, (added.Name, added.GetTextAsync(CancellationToken.None).GetAwaiter().GetResult().ToString()));
                    }

                    foreach (var changedId in projectChanges.GetChangedDocuments())
                    {
                        var newDocument = newSolution.GetDocument(changedId)!;
                        var oldDocument = oldSolution.GetDocument(changedId)!;
                        if (unreplayedDocuments.ContainsKey(changedId))
                        {
                            unreplayedDocuments[changedId] = newDocument.GetTextAsync(CancellationToken.None).GetAwaiter().GetResult().ToString();
                            continue;
                        }

                        var oldText = oldDocument.GetTextAsync(CancellationToken.None).GetAwaiter().GetResult();
                        var diffBase = diffAgainstReparsedText
                            ? oldDocument.WithText(SourceText.From(oldText.ToString(), oldText.Encoding))
                            : oldDocument;
                        IEnumerable<TextChange> textChanges;
                        try
                        {
                            textChanges = newDocument.GetTextChangesAsync(diffBase, CancellationToken.None).GetAwaiter().GetResult();
                        }
                        catch (Exception ex) when (IsKnownSyntaxDifferDefect(ex))
                        {
                            unreplayedDocuments[changedId] = newDocument.GetTextAsync(CancellationToken.None).GetAwaiter().GetResult().ToString();
                            editsPerDocument.Remove(changedId);
                            WriteTestOutputLine(
                                $"RoslynTestKit: host text-edit replay skipped for '{oldDocument.Name}': the syntax differ of " +
                                $"Microsoft.Dynamics.Nav.CodeAnalysis {LoadedSdkVersion.Value} fails on this result (known defect in SDK 14.x and 15.x); " +
                                "this document is verified by workspace apply only.");
                            continue;
                        }
                        catch (Exception ex) when (!(ex is RoslynTestKitException))
                        {
                            throw RoslynTestKitException.FixAllHostDiffFailed(oldDocument.Name, LoadedSdkVersion.Value?.ToString() ?? "unknown", ex);
                        }

                        if (!editsPerDocument.TryGetValue(changedId, out var edits))
                        {
                            edits = new List<TextChange>();
                            editsPerDocument.Add(changedId, edits);
                        }

                        foreach (var change in textChanges)
                        {
                            if (change.Span.End > oldText.Length)
                            {
                                throw RoslynTestKitException.FixAllHostEditOutOfRange(oldDocument.Name, change, oldText.Length);
                            }

                            // The editor receives line/character ranges; round-trip the span through them.
                            var lineSpan = oldText.Lines.GetLinePositionSpan(change.Span);
                            edits.Add(new TextChange(oldText.Lines.GetTextSpan(lineSpan), change.NewText));
                        }
                    }
                }
            }

            var replayedTexts = new Dictionary<DocumentId, string>();
            foreach (var entry in editsPerDocument)
            {
                var oldDocument = oldSolution.GetDocument(entry.Key)!;
                var oldText = oldDocument.GetTextAsync(CancellationToken.None).GetAwaiter().GetResult();
                var orderedEdits = entry.Value
                    .OrderBy(edit => edit.Span.Start)
                    .ThenBy(edit => edit.Span.End)
                    .ToList();
                try
                {
                    replayedTexts[entry.Key] = oldText.WithChanges(orderedEdits).ToString();
                }
                // WithChanges reports unordered or overlapping changes with a plain ArgumentException; any
                // derived exception (null or out-of-range argument) is a different failure and propagates.
                catch (ArgumentException ex) when (ex.GetType() == typeof(ArgumentException))
                {
                    throw RoslynTestKitException.FixAllHostEditsOverlap(oldDocument.Name, orderedEdits);
                }
            }

            string TextOf(Document doc) =>
                replayedTexts.TryGetValue(doc.Id, out var replayed)
                    ? replayed
                    : unreplayedDocuments.TryGetValue(doc.Id, out var unreplayed)
                        ? unreplayed
                        : doc.GetTextAsync(CancellationToken.None).GetAwaiter().GetResult().ToString();

            var otherDocuments = document.Project.Documents
                .Where(doc => doc.Id != document.Id)
                .Select(doc => (doc.Name, TextOf(doc)))
                .Concat(addedDocuments
                    .Where(added => added.Key.ProjectId == document.Project.Id)
                    .Select(added => added.Value));

            return MergeDocuments(TextOf(document), otherDocuments);
        }

        /// <summary>
        /// First (inclusive) <c>Microsoft.Dynamics.Nav.CodeAnalysis</c> file version line whose
        /// <c>SyntaxDiffer.GetCommonEdgeLengths</c> walks the common leading and trailing counts back over line
        /// breaks with a mis-parenthesised condition:
        /// <c>while ((count &gt; 0 &amp;&amp; IsCarriageReturnOrLineFeed(old[count])) || IsCarriageReturnOrLineFeed(new[count])) count--;</c>
        /// for the leading edge, and the same shape with <c>new[new.Length - count - 1]</c> for the trailing edge.
        /// The <c>count &gt; 0</c> guard does not cover the <c>new</c> operand, so when a change's new text starts
        /// (or ends) with a run of line breaks that reaches the position where the compared segments differ,
        /// <c>count</c> goes to -1, the next index is -1 (or <c>new.Length</c>) and the <c>StringBuilder</c> indexer
        /// throws <see cref="IndexOutOfRangeException"/>, which neither <c>Document.GetTextChangesAsync</c> nor the
        /// editor host catches for a syntax-root result.
        /// The 13.x differ has no walk-back (tag <c>v13.1.16.16524</c> of the decompiled SDK source); 14.x and
        /// 15.x releases have the defect (tags <c>v14.0.17.47112</c>, <c>v15.2.24.57679</c>). Early 14.0
        /// prereleases (14.0.14, 14.0.15 and some 14.0.16 builds) have no walk-back yet; they never throw this
        /// exception, so their inclusion in the range is harmless.
        /// </summary>
        private static readonly Version SyntaxDifferDefectFirstVersion = new Version(14, 0);

        /// <summary>
        /// Exclusive end of the affected range: the first release line where the fixed walk-back
        /// <c>while (count &gt; 0 &amp;&amp; (IsCarriageReturnOrLineFeed(old[count]) || IsCarriageReturnOrLineFeed(new[count])))</c>
        /// is present (release tag <c>v16.0.27.57058</c>). 16.0 prereleases below 16.0.25 (for example
        /// <c>v16.0.21.53261-prerelease</c>, <c>v16.0.24.41895-prerelease</c>) still have the defect; on those the
        /// exception is not skipped and surfaces as <see cref="RoslynTestKitException.FixAllHostDiffFailed"/>.
        /// </summary>
        private static readonly Version SyntaxDifferDefectFixedVersion = new Version(16, 0);

        /// <summary>
        /// File version (<see cref="AssemblyFileVersionAttribute"/>) of the loaded
        /// <c>Microsoft.Dynamics.Nav.CodeAnalysis</c> assembly, or null when it cannot be read.
        /// </summary>
        private static readonly Lazy<Version?> LoadedSdkVersion = new Lazy<Version?>(() =>
        {
            var attribute = typeof(Microsoft.Dynamics.Nav.CodeAnalysis.Syntax.SyntaxTree).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
            return attribute != null && Version.TryParse(attribute.Version, out var version) ? version : null;
        });

        /// <summary>
        /// True when <paramref name="exception"/> is the <see cref="IndexOutOfRangeException"/> of the SDK
        /// syntax differ defect (see <see cref="SyntaxDifferDefectFirstVersion"/>) and the loaded SDK is in the
        /// affected range. The origin is recognised by a <c>Microsoft.Dynamics.Nav.CodeAnalysis.SyntaxDiffer</c>
        /// frame in the stack trace: the differ is internal and the throwing frame is the <c>StringBuilder</c>
        /// indexer, so there is no type to match. The match assumes the differ's frames are not inlined away;
        /// a miss fails loudly as <see cref="RoslynTestKitException.FixAllHostDiffFailed"/>, never passes silently.
        /// </summary>
        private static bool IsKnownSyntaxDifferDefect(Exception exception)
        {
            var version = LoadedSdkVersion.Value;
            return exception is IndexOutOfRangeException
                && version != null
                && version >= SyntaxDifferDefectFirstVersion
                && version < SyntaxDifferDefectFixedVersion
                && (exception.StackTrace?.Contains("Microsoft.Dynamics.Nav.CodeAnalysis.SyntaxDiffer") ?? false);
        }

        /// <summary>
        /// Writes a line to the output of the running NUnit test. Never throws: without a test context the
        /// note is dropped.
        /// </summary>
        private static void WriteTestOutputLine(string message)
        {
            try
            {
                TestContext.Out.WriteLine(message);
            }
            catch (Exception)
            {
                // The note is informational only.
            }
        }

        /// <summary>
        /// Joins the main document and the other documents of its project (ordered by descending name,
        /// which restores the <c>/*EOD*/</c> segment order of the markup) into one string.
        /// </summary>
        private static string MergeDocuments(string mainDocumentText, IEnumerable<(string Name, string Text)> otherDocuments)
        {
            var mergedDocumentBuilder = new StringBuilder();
            mergedDocumentBuilder.Append(mainDocumentText);

            foreach (var (_, text) in otherDocuments.OrderByDescending(x => x.Name))
            {
                mergedDocumentBuilder.AppendLine($"{Environment.NewLine}{BaseTestFixture.FileSeparator}");
                mergedDocumentBuilder.Append(text);
            }

            return mergedDocumentBuilder.ToString();
        }

        private static void CompareWithExpected(string actualCode, string expectedCode, string? stage)
        {
            actualCode = NormalizeLineEndings(actualCode);
            expectedCode = NormalizeLineEndings(expectedCode);

            if (actualCode != expectedCode)
            {
                DiffHelper.TryToReportDiffWithExternalTool(expectedCode, actualCode);
                var diff = DiffHelper.GenerateInlineDiff(expectedCode, actualCode);
                throw new TransformedCodeDifferentThanExpectedException(actualCode, expectedCode, diff, stage);
            }
        }

        private static string NormalizeLineEndings(string text)
        {
            return text.Replace("\r\n", "\n");
        }
    }
}
