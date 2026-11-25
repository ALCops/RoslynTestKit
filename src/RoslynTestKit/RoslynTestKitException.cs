using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Dynamics.Nav.CodeAnalysis.CodeActions;
using Microsoft.Dynamics.Nav.CodeAnalysis.CodeFixes;
using Microsoft.Dynamics.Nav.CodeAnalysis.Diagnostics;
using Microsoft.Dynamics.Nav.CodeAnalysis.Text;
using Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces;
using Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces.Completion;
using RoslynTestKit.CodeActionLocators;
using RoslynTestKit.Utils;

namespace RoslynTestKit
{
    public class RoslynTestKitException : Exception
    {
        public RoslynTestKitException(string message) : base(message)
        {
        }

        public static RoslynTestKitException UnexpectedDiagnostic(List<Diagnostic> unexpectedDiagnostics)
        {
            var messages = unexpectedDiagnostics.Select(d =>
            {
                var position = d.Location.GetLineSpan().StartLinePosition;
                return $"Found reported diagnostic '{d.Id}' in spite of the expectations at Line:{position.Line} Col:{position.Character}. Details: {d.GetMessage()}";
            });
            var description = string.Join("\r\n", messages);
            return new RoslynTestKitException(description);
        }

        public static Exception UnexpectedErrorDiagnostic(IReadOnlyList<Diagnostic> errors)
        {
            var messages = errors.MergeWithNewLines(x => x.ToString());
            return new RoslynTestKitException($"Input document contains errors: \r\n {messages}");
        }

        internal static Exception ExceptionInAnalyzer(
            Diagnostic diagnostic)
        {
            var message = diagnostic.Descriptor.Description.ToString();
            return new RoslynTestKitException(message);
        }

        public static RoslynTestKitException DiagnosticNotFound(string diagnosticId, IDiagnosticLocator locator, Diagnostic[] reportedDiagnostics)
        {
            var reportedDiagnosticInfo = reportedDiagnostics.MergeWithComma(x => x.Id, title: "Reported issues: ");
            var message = $"There is no issue reported for {diagnosticId} at {locator.Description()}.{reportedDiagnosticInfo}";
            return new RoslynTestKitException(message);
        }

        public static RoslynTestKitException CodeFixNotFound(ICodeActionSelector codeActionSelector, ImmutableArray<CodeAction> codeFixes, IDiagnosticLocator locator)
        {
            var codeFixDescription = GetActionsDescription(codeFixes, $" Found only {codeFixes.Length} CodeFixes: ");
            var message = $"Cannot find CodeFix {codeActionSelector} at {locator.Description()}.{codeFixDescription}";
            return new RoslynTestKitException(message);
        }

        public static RoslynTestKitException UnexpectedCodeFixFound(ImmutableArray<CodeAction> codeFixes, IDiagnosticLocator locator)
        {
            var codeFixDescription = GetActionsDescription(codeFixes, "Reported fixes: ");
            var message = $"Found unexpected CodeFixes at {locator.Description()}.{codeFixDescription}";
            return new RoslynTestKitException(message);
        }

        public static RoslynTestKitException CodeRefactoringNotFound(ICodeActionSelector codeActionSelector, ImmutableArray<CodeAction> codeRefactorings, IDiagnosticLocator locator)
        {
            var refactoringDescriptions = GetActionsDescription(codeRefactorings, $" Found only {codeRefactorings.Length} CodeRefactorings: ");
            var message = $"Cannot find CodeRefactoring {codeActionSelector}  at {locator.Description()}.{refactoringDescriptions}";
            return new RoslynTestKitException(message);
        }

        public static RoslynTestKitException UnexpectedCodeRefactorings(ImmutableArray<CodeAction> codeRefactorings)
        {
            var refactoringDescriptions = GetActionsDescription(codeRefactorings);
            return new RoslynTestKitException($"Found reported CodeRefactorings '{refactoringDescriptions}' in spite of the expectations ");
        }

        private static string GetActionsDescription(ImmutableArray<CodeAction> codeFixes, string? title = null)
        {
            if (codeFixes.Length == 0)
            {
                return string.Empty;
            }

            return title + Environment.NewLine + string.Join(Environment.NewLine, codeFixes.SelectMany((x, index) =>
            {
                if (NestedCodeActionHelper.TryGetNestedAction(x) is { } nestedAction)
                {
                    return nestedAction.Select(n => $"[{index}] = {x.Title} -> {n.Title}").ToArray();
                }

                return new[] { $"[{index}] = {x.Title}" };
            })) + Environment.NewLine;
        }

        public static RoslynTestKitException NoOperationForCodeAction(CodeAction codeAction)
        {
            return new RoslynTestKitException($"There is no operation associated with '{codeAction.Title}'");
        }

        public static RoslynTestKitException MoreThanOneOperationForCodeAction(CodeAction codeAction, List<CodeActionOperation> operations)
        {
            var foundOperationDescriptions = operations.MergeWithComma(x => x.Title ?? "<unnamed>", title: " Found operations: ");
            return new RoslynTestKitException($"There is more than one operation associated with '{codeAction.Title}'.{foundOperationDescriptions}");
        }

        public static Exception CannotFindSuggestion(IReadOnlyList<string> missingCompletion, ImmutableArray<CompletionItem> resultItems, IDiagnosticLocator locator)
        {
            var foundSuggestionDescription = resultItems.MergeAsBulletList(x => x.DisplayText, title: "\r\nFound suggestions:\r\n");
            return new RoslynTestKitException($"Cannot get suggestions:\r\n{missingCompletion.MergeAsBulletList()}\r\nat{locator.Description()}{foundSuggestionDescription}");
        }

        public static RoslynTestKitException FixAllProviderNotFound(string codeFixProviderName)
        {
            return new RoslynTestKitException(
                $"CodeFixProvider '{codeFixProviderName}' does not support FixAll (GetFixAllProvider() returned null). " +
                "Ensure the CodeFixProvider overrides GetFixAllProvider() and returns a non-null FixAllProvider.");
        }

        public static RoslynTestKitException FixAllDiagnosticCountMismatch(int expectedCount, int actualCount, string diagnosticId, IReadOnlyList<Diagnostic> foundDiagnostics)
        {
            return FixAllDiagnosticCountMismatch(expectedCount, actualCount, diagnosticId, foundDiagnostics, solution: null);
        }

        /// <param name="solution">When non-null, each listed diagnostic also names the document it was reported in.</param>
        public static RoslynTestKitException FixAllDiagnosticCountMismatch(int expectedCount, int actualCount, string diagnosticId, IReadOnlyList<Diagnostic> foundDiagnostics, Solution? solution)
        {
            var diagnosticInfo = foundDiagnostics.MergeWithNewLines(d =>
            {
                var position = d.Location.GetLineSpan().StartLinePosition;
                var tree = d.Location.SourceTree;
                var documentInfo = solution != null && tree != null ? $" {solution.GetDocument(tree)?.Name ?? "<unknown document>"}" : "";
                return $"  [{d.Id}]{documentInfo} Line:{position.Line} Col:{position.Character} - {d.GetMessage()}";
            });
            return new RoslynTestKitException(
                $"Expected {expectedCount} diagnostics for '{diagnosticId}' (based on [| |] markers) but found {actualCount}.\r\n" +
                $"Diagnostics found:\r\n{diagnosticInfo}");
        }

        public static RoslynTestKitException FixAllReturnedNoAction(string? equivalenceKey)
        {
            var keyInfo = equivalenceKey != null ? $" with equivalence key '{equivalenceKey}'" : "";
            return new RoslynTestKitException(
                $"FixAllProvider.GetFixAsync() returned null{keyInfo}. " +
                "The FixAll operation produced no code action.");
        }

        public static RoslynTestKitException FixAllScopeNotSupported(string fixAllProviderName, FixAllScope scope, IEnumerable<FixAllScope> supportedScopes)
        {
            return new RoslynTestKitException(
                $"FixAllProvider '{fixAllProviderName}' does not support FixAll scope '{scope}'. " +
                $"Supported scopes: {string.Join(", ", supportedScopes)}.");
        }

        public static RoslynTestKitException FixAllHostEditsOverlap(string documentName, IReadOnlyList<TextChange> edits)
        {
            var editInfo = edits.MergeWithNewLines(e => $"  {DescribeEdit(e)}");
            return new RoslynTestKitException(
                $"The text edits the editor host would send for '{documentName}' overlap and cannot be applied as one batch.\r\n" +
                $"Edits (ordered by start):\r\n{editInfo}");
        }

        public static RoslynTestKitException FixAllHostEditOutOfRange(string documentName, TextChange edit, int textLength)
        {
            return new RoslynTestKitException(
                $"A text edit the editor host would send for '{documentName}' lies outside the original text (length {textLength}): {DescribeEdit(edit)}");
        }

        private static string DescribeEdit(TextChange edit)
        {
            var newText = (edit.NewText ?? string.Empty).Replace("\r", "\\r").Replace("\n", "\\n");
            return $"[{edit.Span.Start}..{edit.Span.End}) -> \"{newText}\"";
        }

        public static RoslynTestKitException InvalidRuleSet(string ruleSetPath, IReadOnlyList<Diagnostic> diagnostics)
        {
            var details = diagnostics.MergeWithNewLines(d => $"  [{d.Id}] {d.GetMessage()}");
            return new RoslynTestKitException(
                $"Failed to load ruleset '{ruleSetPath}'.\r\n{details}");
        }
    }
}