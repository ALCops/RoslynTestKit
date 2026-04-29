using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.CodeActions;
using Microsoft.Dynamics.Nav.CodeAnalysis.CodeFixes;
using Microsoft.Dynamics.Nav.CodeAnalysis.Diagnostics;
using Microsoft.Dynamics.Nav.CodeAnalysis.Text;
using Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces;
using RoslynTestKit.CodeActionLocators;
using RoslynTestKit.Utils;
using Document = Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces.Document;

namespace RoslynTestKit
{
    public abstract class CodeFixTestFixture : BaseTestFixture
    {
        protected abstract CodeFixProvider CreateProvider();

        protected virtual IReadOnlyCollection<DiagnosticAnalyzer>? CreateAdditionalAnalyzers() => null;

        public void NoCodeFix(string markupCode, string diagnosticId)
        {
            var markup = new CodeMarkup(markupCode);
            var document = CreateDocumentFromCode(markup.Code);
            NoCodeFix(document, diagnosticId, markup.Locator);
        }

        public void NoCodeFixAtLine(string code, string diagnosticId, int line)
        {
            var document = CreateDocumentFromCode(code);
            var locator = LineLocator.FromCode(code, line);
            NoCodeFix(document, diagnosticId, locator);
        }

        public void NoCodeFixAtLine(string code, DiagnosticDescriptor descriptor, int line)
        {
            var document = CreateDocumentFromCode(code);
            var locator = LineLocator.FromCode(code, line);
            var diagnostic = FindOrCreateDiagnosticForDescriptor(document, descriptor, locator);
            NoCodeFix(document, diagnostic, locator);
        }
        public void NoCodeFix(string markupCode, DiagnosticDescriptor descriptor)
        {
            var markup = new CodeMarkup(markupCode);
            var document = CreateDocumentFromCode(markup.Code);
            var diagnostic = FindOrCreateDiagnosticForDescriptor(document, descriptor, markup.Locator);
            NoCodeFix(document, diagnostic, markup.Locator);
        }

        public void NoCodeFix(Document document, DiagnosticDescriptor descriptor, TextSpan span)
        {
            var locator = new TextSpanLocator(span);
            var diagnostic = FindOrCreateDiagnosticForDescriptor(document, descriptor, locator);
            NoCodeFix(document, diagnostic, locator);
        }

        public void TestCodeFix(string markupCode, string expected, string diagnosticId, int codeFixIndex = 0)
        {
            var codeActionSelector = new ByIndexCodeActionSelector(codeFixIndex);
            TestCodeFix(markupCode, expected, diagnosticId, codeActionSelector);
        }

        public void TestCodeFix(string markupCode, string expected, string diagnosticId, string title)
        {
            var codeActionSelector = new ByTitleCodeActionSelector(title);
            TestCodeFix(markupCode, expected, diagnosticId, codeActionSelector);
        }

        public void TestCodeFix(string markupCode, string expected, string diagnosticId, ICodeActionSelector actionSelector)
        {
            var markup = new CodeMarkup(markupCode);
            var document = CreateDocumentFromCode(markup.Code);
            TestCodeFix(document, expected, diagnosticId, markup.Locator, actionSelector);
        }

        public void TestCodeFixAtLine(string code, string expected, string diagnosticId, int line, int codeFixIndex = 0)
        {
            var document = CreateDocumentFromCode(code);
            var locator = LineLocator.FromCode(code, line);
            TestCodeFix(document, expected, diagnosticId, locator, new ByIndexCodeActionSelector(codeFixIndex));
        }

        public void TestCodeFixAtLine(string code, string expected, DiagnosticDescriptor descriptor, int line, int codeFixIndex = 0)
        {
            var document = CreateDocumentFromCode(code);
            var locator = LineLocator.FromCode(code, line);
            var diagnostic = FindOrCreateDiagnosticForDescriptor(document, descriptor, locator);
            TestCodeFix(document, expected, diagnostic, locator, new ByIndexCodeActionSelector(codeFixIndex));
        }
        public void TestCodeFix(string markupCode, string expected, DiagnosticDescriptor descriptor, int codeFixIndex = 0)
        {
            var markup = new CodeMarkup(markupCode);
            var document = CreateDocumentFromCode(markup.Code);
            var diagnostic = FindOrCreateDiagnosticForDescriptor(document, descriptor, markup.Locator);
            TestCodeFix(document, expected, diagnostic, markup.Locator, new ByIndexCodeActionSelector(codeFixIndex));
        }

        public void TestCodeFix(Document document, string expected, DiagnosticDescriptor descriptor, TextSpan span, int codeFixIndex = 0)
        {
            var locator = new TextSpanLocator(span);
            var diagnostic = FindOrCreateDiagnosticForDescriptor(document, descriptor, locator);
            TestCodeFix(document, expected, diagnostic, locator, new ByIndexCodeActionSelector(codeFixIndex));
        }

        /// <summary>
        /// Tests the FixAll operation for a given diagnostic ID. The markup code must contain
        /// multiple <c>[| |]</c> markers, one for each expected diagnostic. The method asserts that
        /// the analyzer produces exactly as many diagnostics as there are markers, then invokes the
        /// CodeFix's <see cref="FixAllProvider"/> at <see cref="FixAllScope.Document"/> scope and
        /// compares the result against the expected code.
        /// </summary>
        /// <param name="markupCode">Code with <c>[| |]</c> markers at each expected diagnostic location.</param>
        /// <param name="expected">The expected code after all fixes have been applied.</param>
        /// <param name="diagnosticId">The diagnostic ID to fix (e.g. "AC0031").</param>
        /// <param name="codeFixIndex">Index of the code fix to select for equivalence key auto-detection (default: 0).</param>
        /// <param name="equivalenceKey">Optional explicit equivalence key. When null, auto-detected from the first diagnostic's code fix.</param>
        public void TestFixAll(string markupCode, string expected, string diagnosticId, int codeFixIndex = 0, string? equivalenceKey = null)
        {
            var markup = new CodeMarkup(markupCode);
            var document = CreateDocumentFromCode(markup.Code);
            var allDiagnostics = GetAllReportedDiagnostics(document).ToList();
            var matchingDiagnostics = allDiagnostics.Where(d => d.Id == diagnosticId).ToList();

            if (matchingDiagnostics.Count != markup.AllLocators.Count)
            {
                throw RoslynTestKitException.FixAllDiagnosticCountMismatch(
                    markup.AllLocators.Count, matchingDiagnostics.Count, diagnosticId, matchingDiagnostics);
            }

            TestFixAll(document, expected, matchingDiagnostics, codeFixIndex, equivalenceKey);
        }

        /// <summary>
        /// Tests the FixAll operation for a given <see cref="DiagnosticDescriptor"/>. The markup code must
        /// contain multiple <c>[| |]</c> markers, one for each expected diagnostic.
        /// </summary>
        /// <param name="markupCode">Code with <c>[| |]</c> markers at each expected diagnostic location.</param>
        /// <param name="expected">The expected code after all fixes have been applied.</param>
        /// <param name="descriptor">The diagnostic descriptor to fix.</param>
        /// <param name="codeFixIndex">Index of the code fix to select for equivalence key auto-detection (default: 0).</param>
        /// <param name="equivalenceKey">Optional explicit equivalence key. When null, auto-detected from the first diagnostic's code fix.</param>
        public void TestFixAll(string markupCode, string expected, DiagnosticDescriptor descriptor, int codeFixIndex = 0, string? equivalenceKey = null)
        {
            TestFixAll(markupCode, expected, descriptor.Id, codeFixIndex, equivalenceKey);
        }

        private void TestFixAll(Document document, string expected, IReadOnlyList<Diagnostic> diagnostics, int codeFixIndex, string? equivalenceKey)
        {
            var provider = CreateProvider();
            var fixAllProvider = provider.GetFixAllProvider();
            if (fixAllProvider is null)
            {
                throw RoslynTestKitException.FixAllProviderNotFound(provider.GetType().Name);
            }

            if (equivalenceKey is null)
            {
                var firstDiagnostic = diagnostics[0];
                var codeFixes = GetCodeFixes(document, firstDiagnostic);
                if (codeFixes.Length > codeFixIndex)
                {
                    equivalenceKey = codeFixes[codeFixIndex].EquivalenceKey;
                }
            }

            var diagnosticIds = diagnostics.Select(d => d.Id).Distinct();
            var diagnosticProvider = new TestDiagnosticProvider(diagnostics.ToImmutableArray());

            var fixAllContext = new FixAllContext(
                document,
                provider,
                FixAllScope.Document,
                equivalenceKey,
                diagnosticIds,
                diagnosticProvider,
                CancellationToken.None);

            var fixAllAction = fixAllProvider.GetFixAsync(fixAllContext).GetAwaiter().GetResult();
            if (fixAllAction is null)
            {
                throw RoslynTestKitException.FixAllReturnedNoAction(equivalenceKey);
            }

            Verify.CodeAction(fixAllAction, document, expected);
        }

        private void TestCodeFix(Document document, string expected, string diagnosticId, IDiagnosticLocator locator, ICodeActionSelector codeActionSelector)
        {
            var diagnostic = GetDiagnostic(document, diagnosticId, locator);
            TestCodeFix(document, expected, diagnostic, locator, codeActionSelector);
        }

        private void NoCodeFix(Document document, string diagnosticId, IDiagnosticLocator locator)
        {
            var diagnostic = GetDiagnostic(document, diagnosticId, locator);
            NoCodeFix(document, diagnostic, locator);
        }

        private void NoCodeFix(Document document, Diagnostic diagnostic, IDiagnosticLocator locator)
        {
            var codeFixes = GetCodeFixes(document, diagnostic);
            if (codeFixes.Length != 0)
            {
                throw RoslynTestKitException.UnexpectedCodeFixFound(codeFixes, locator);
            }
        }

        private Diagnostic GetDiagnostic(Document document, string diagnosticId, IDiagnosticLocator locator)
        {
            var reportedDiagnostics = GetReportedDiagnostics(document, locator).ToArray();
            var diagnostic = reportedDiagnostics.FirstOrDefault(x => x.Id == diagnosticId);
            if (diagnostic == null)
            {
                throw RoslynTestKitException.DiagnosticNotFound(diagnosticId, locator, reportedDiagnostics);
            }

            return diagnostic;
        }

        private void TestCodeFix(Document document, string expected, Diagnostic diagnostic, IDiagnosticLocator locator, ICodeActionSelector codeActionSelector)
        {
            var codeFixes = GetCodeFixes(document, diagnostic);
            var codeAction = codeActionSelector.Find(codeFixes);
            if (codeAction is null)
            {
                throw RoslynTestKitException.CodeFixNotFound(codeActionSelector, codeFixes, locator);
            }
            Verify.CodeAction(codeAction, document, expected);
        }

        private IEnumerable<Diagnostic> GetReportedDiagnostics(Document document, IDiagnosticLocator locator)
        {
            var allReportedDiagnostics = GetAllReportedDiagnostics(document);
            foreach (var diagnostic in allReportedDiagnostics)
            {
                if (locator.Match(diagnostic.Location))
                {
                    yield return diagnostic;
                }
                else if (ThrowsWhenInputDocumentContainsError && diagnostic.Severity == DiagnosticSeverity.Error)
                {
                    throw new InvalidOperationException($"Input document contains unexpected error: {diagnostic.GetMessage()}");
                }
            }

        }

        private IEnumerable<Diagnostic> GetAllReportedDiagnostics(Document document)
        {
            var additionalAnalyzers = CreateAdditionalAnalyzers();
            if (additionalAnalyzers != null)
            {
                var documentTree = document.GetSyntaxTreeAsync().GetAwaiter().GetResult();

                var compilation = document.Project.GetCompilationAsync().GetAwaiter().GetResult();
                if (compilation is null)
                {
                    throw new InvalidOperationException("Unable to get compilation from document project.");
                }

                if (FileSystem != null)
                {
                    compilation = compilation.WithFileSystem(FileSystem);
                }

                return compilation
                    .WithAnalyzers(additionalAnalyzers.ToImmutableArray(), new AnalyzerOptions(this.AdditionalFiles?.ToImmutableArray() ?? ImmutableArray<AdditionalText>.Empty))
                    .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult()
                    .Where(x => x.Location.SourceTree == documentTree);
            }

            var semanticModel = document.GetSemanticModelAsync().GetAwaiter().GetResult();
            if (semanticModel == null)
            {
                throw new InvalidOperationException("Unable to get semantic model from document.");
            }
            return semanticModel.GetDiagnostics();
        }

        private ImmutableArray<CodeAction> GetCodeFixes(Document document, Diagnostic diagnostic)
        {
            var builder = ImmutableArray.CreateBuilder<CodeAction>();
            var context = new CodeFixContext(document, diagnostic, (a, _) => builder.Add(a), CancellationToken.None);
            var provider = CreateProvider();
            provider.RegisterCodeFixesAsync(context).GetAwaiter().GetResult();
            return builder.ToImmutable();
        }

        private Diagnostic FindOrCreateDiagnosticForDescriptor(Document document, DiagnosticDescriptor descriptor, IDiagnosticLocator locator)
        {
            var reportedDiagnostics = GetReportedDiagnostics(document, locator).ToList();
            var diagnostic = reportedDiagnostics.FirstOrDefault(x => x.Id == descriptor.Id);
            if (diagnostic != null)
            {
                return diagnostic;
            }

            var tree = document.GetSyntaxTreeAsync(CancellationToken.None).Result;
            if (tree == null)
            {
                throw new InvalidOperationException("Unable to get syntax tree from document.");
            }
            return Diagnostic.Create(descriptor, Location.Create(tree, locator.GetSpan()));
        }
    }
}
