using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
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
            var diagnostics = FindOrCreateDiagnosticForDescriptor(document, descriptor, locator);
            NoCodeFix(document, diagnostics, locator);
        }
        public void NoCodeFix(string markupCode, DiagnosticDescriptor descriptor)
        {
            var markup = new CodeMarkup(markupCode);
            var document = CreateDocumentFromCode(markup.Code);
            var diagnostics = FindOrCreateDiagnosticForDescriptor(document, descriptor, markup.Locator);
            NoCodeFix(document, diagnostics, markup.Locator);
        }

        public void NoCodeFix(Document document, DiagnosticDescriptor descriptor, TextSpan span)
        {
            var locator = new TextSpanLocator(span);
            var diagnostics = FindOrCreateDiagnosticForDescriptor(document, descriptor, locator);
            NoCodeFix(document, diagnostics, locator);
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
            var diagnostics = FindOrCreateDiagnosticForDescriptor(document, descriptor, locator);
            TestCodeFix(document, expected, diagnostics, locator, new ByIndexCodeActionSelector(codeFixIndex));
        }
        public void TestCodeFix(string markupCode, string expected, DiagnosticDescriptor descriptor, int codeFixIndex = 0)
        {
            var markup = new CodeMarkup(markupCode);
            var document = CreateDocumentFromCode(markup.Code);
            var diagnostics = FindOrCreateDiagnosticForDescriptor(document, descriptor, markup.Locator);
            TestCodeFix(document, expected, diagnostics, markup.Locator, new ByIndexCodeActionSelector(codeFixIndex));
        }

        public void TestCodeFix(Document document, string expected, DiagnosticDescriptor descriptor, TextSpan span, int codeFixIndex = 0)
        {
            var locator = new TextSpanLocator(span);
            var diagnostics = FindOrCreateDiagnosticForDescriptor(document, descriptor, locator);
            TestCodeFix(document, expected, diagnostics, locator, new ByIndexCodeActionSelector(codeFixIndex));
        }

        /// <summary>
        /// Tests the FixAll operation for a given diagnostic ID. The markup code must contain
        /// multiple <c>[| |]</c> markers, one for each expected diagnostic. The method asserts that
        /// the analyzer produces exactly as many diagnostics as there are markers, then invokes the
        /// CodeFix's <see cref="FixAllProvider"/> at the requested <paramref name="scope"/> and
        /// compares the result against the expected code.
        /// </summary>
        /// <remarks>
        /// <para>
        /// At <see cref="FixAllScope.Document"/> scope the diagnostics of the main document are computed
        /// once and handed to the provider. At <see cref="FixAllScope.Project"/> and
        /// <see cref="FixAllScope.Workspace"/> scope the markers of all <c>/*EOD*/</c> segments are counted
        /// against the diagnostics of the whole test project, and every diagnostic request of the provider
        /// recomputes them from the project's compilation, like the editor's "Fix All in Project". Unlike the
        /// editor, which passes empty analyzer options, the configured <c>AdditionalFiles</c> are kept.
        /// Workspace scope also fixes the dependency projects, but only the test project is compared.
        /// </para>
        /// <para>
        /// With <see cref="FixAllHostVerification.TextEdits"/> (the default) the result is verified twice:
        /// the text edits the editor host would send are replayed against the original documents, then
        /// the operations are applied to the workspace. Both results must equal <paramref name="expected"/>.
        /// </para>
        /// </remarks>
        /// <param name="markupCode">Code with <c>[| |]</c> markers at each expected diagnostic location.</param>
        /// <param name="expected">The expected code after all fixes have been applied, with every <c>/*EOD*/</c> segment.</param>
        /// <param name="diagnosticId">The diagnostic ID to fix (e.g. "AC0031").</param>
        /// <param name="codeFixIndex">Index of the code fix to select for equivalence key auto-detection (default: 0).</param>
        /// <param name="equivalenceKey">Optional explicit equivalence key. When null, auto-detected from the first diagnostic's code fix.</param>
        /// <param name="scope">The FixAll scope to invoke the provider with (default: <see cref="FixAllScope.Document"/>).</param>
        /// <param name="hostVerification">How the result is verified (default: <see cref="FixAllHostVerification.TextEdits"/>).</param>
        public void TestFixAll(string markupCode, string expected, string diagnosticId, int codeFixIndex = 0, string? equivalenceKey = null,
            FixAllScope scope = FixAllScope.Document, FixAllHostVerification hostVerification = FixAllHostVerification.TextEdits)
        {
            var markup = new CodeMarkup(markupCode);
            var document = CreateDocumentFromCode(markup.Code);
            var matchingDiagnostics = scope == FixAllScope.Document
                ? GetAllReportedDiagnostics(document).Where(d => d.Id == diagnosticId).ToList()
                : GetProjectDiagnosticsLikeHost(document.Project, new HashSet<string> { diagnosticId }, CancellationToken.None).ToList();

            if (matchingDiagnostics.Count != markup.AllLocators.Count)
            {
                throw RoslynTestKitException.FixAllDiagnosticCountMismatch(
                    markup.AllLocators.Count, matchingDiagnostics.Count, diagnosticId, matchingDiagnostics, document.Project.Solution);
            }

            TestFixAll(document, expected, matchingDiagnostics, codeFixIndex, equivalenceKey, scope, hostVerification);
        }

        /// <summary>
        /// Tests the FixAll operation for a given <see cref="DiagnosticDescriptor"/>. The markup code must
        /// contain multiple <c>[| |]</c> markers, one for each expected diagnostic. See
        /// <see cref="TestFixAll(string, string, string, int, string?, FixAllScope, FixAllHostVerification)"/>
        /// for how the scope and the verification behave.
        /// </summary>
        /// <param name="markupCode">Code with <c>[| |]</c> markers at each expected diagnostic location.</param>
        /// <param name="expected">The expected code after all fixes have been applied, with every <c>/*EOD*/</c> segment.</param>
        /// <param name="descriptor">The diagnostic descriptor to fix.</param>
        /// <param name="codeFixIndex">Index of the code fix to select for equivalence key auto-detection (default: 0).</param>
        /// <param name="equivalenceKey">Optional explicit equivalence key. When null, auto-detected from the first diagnostic's code fix.</param>
        /// <param name="scope">The FixAll scope to invoke the provider with (default: <see cref="FixAllScope.Document"/>).</param>
        /// <param name="hostVerification">How the result is verified (default: <see cref="FixAllHostVerification.TextEdits"/>).</param>
        public void TestFixAll(string markupCode, string expected, DiagnosticDescriptor descriptor, int codeFixIndex = 0, string? equivalenceKey = null,
            FixAllScope scope = FixAllScope.Document, FixAllHostVerification hostVerification = FixAllHostVerification.TextEdits)
        {
            TestFixAll(markupCode, expected, descriptor.Id, codeFixIndex, equivalenceKey, scope, hostVerification);
        }

        // The two overloads below keep the signatures of releases before the scope and host-verification
        // parameters existed, so assemblies compiled against them still bind. Their parameters are
        // required on purpose: a call that omits an argument binds only to the overloads above, and a
        // call that passes all five arguments prefers these (no default substituted), so no call shape
        // is ambiguous. They verify like the defaults above.

        /// <summary>Binary-compatibility forwarder; see the overload with <see cref="FixAllScope"/>.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void TestFixAll(string markupCode, string expected, string diagnosticId, int codeFixIndex, string? equivalenceKey)
        {
            TestFixAll(markupCode, expected, diagnosticId, codeFixIndex, equivalenceKey, FixAllScope.Document, FixAllHostVerification.TextEdits);
        }

        /// <summary>Binary-compatibility forwarder; see the overload with <see cref="FixAllScope"/>.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void TestFixAll(string markupCode, string expected, DiagnosticDescriptor descriptor, int codeFixIndex, string? equivalenceKey)
        {
            TestFixAll(markupCode, expected, descriptor.Id, codeFixIndex, equivalenceKey, FixAllScope.Document, FixAllHostVerification.TextEdits);
        }

        private void TestFixAll(Document document, string expected, IReadOnlyList<Diagnostic> diagnostics, int codeFixIndex, string? equivalenceKey,
            FixAllScope scope, FixAllHostVerification hostVerification)
        {
            var provider = CreateProvider();
            var fixAllProvider = provider.GetFixAllProvider();
            if (fixAllProvider is null)
            {
                throw RoslynTestKitException.FixAllProviderNotFound(provider.GetType().Name);
            }

            var supportedScopes = fixAllProvider.GetSupportedFixAllScopes().ToList();
            if (!supportedScopes.Contains(scope))
            {
                throw RoslynTestKitException.FixAllScopeNotSupported(fixAllProvider.GetType().Name, scope, supportedScopes);
            }

            if (equivalenceKey is null)
            {
                // One diagnostic per context, as the BatchFixer hands each diagnostic to the provider on its own.
                // Outside Document scope the first diagnostic can live in another document of the project.
                var firstDiagnostic = diagnostics[0];
                var firstTree = firstDiagnostic.Location.SourceTree;
                var probeDocument = (firstTree is null ? null : document.Project.Solution.GetDocument(firstTree)) ?? document;
                var codeFixes = GetCodeFixes(probeDocument, ImmutableArray.Create(firstDiagnostic));
                if (codeFixes.Length > codeFixIndex)
                {
                    equivalenceKey = codeFixes[codeFixIndex].EquivalenceKey;
                }
            }

            var diagnosticIds = diagnostics.Select(d => d.Id).Distinct();
            FixAllContext.DiagnosticProvider diagnosticProvider = scope == FixAllScope.Document
                ? (FixAllContext.DiagnosticProvider)new TestDiagnosticProvider(diagnostics.ToImmutableArray())
                : new EagerProjectDiagnosticProvider(GetProjectDiagnosticsLikeHost);

            var fixAllContext = new FixAllContext(
                document,
                provider,
                scope,
                equivalenceKey,
                diagnosticIds,
                diagnosticProvider,
                CancellationToken.None);

            var fixAllAction = fixAllProvider.GetFixAsync(fixAllContext).GetAwaiter().GetResult();
            if (fixAllAction is null)
            {
                throw RoslynTestKitException.FixAllReturnedNoAction(equivalenceKey);
            }

            Verify.CodeAction(fixAllAction, document, expected, hostVerification);
        }

        /// <summary>
        /// Computes the diagnostics of <paramref name="project"/> the way the editor host's eager FixAll
        /// provider does. When at least one analyzer supports one of <paramref name="diagnosticIds"/>, the host
        /// runs only those analyzers and concatenates two lists it gets back: the compiler diagnostics of the
        /// compilation (<c>newCompilation.GetDiagnostics()</c>) and the analyzer diagnostics. When no analyzer
        /// supports the ids, it uses the compiler diagnostics alone. Either way the result is filtered to
        /// <paramref name="diagnosticIds"/>. Compiler and analyzer diagnostics are separate lists, so a
        /// diagnostic is not counted twice. The compilation is taken from <paramref name="project"/> itself, so the diagnostics'
        /// syntax trees belong to that project snapshot and map back to its documents.
        /// Deviation from the host: the host passes empty analyzer options; the configured
        /// <see cref="BaseTestFixture.AdditionalFiles"/> are kept here so analyzers see the same input as
        /// in the other fixture methods.
        /// </summary>
        private ImmutableArray<Diagnostic> GetProjectDiagnosticsLikeHost(Project project, ISet<string> diagnosticIds, CancellationToken cancellationToken)
        {
            var compilation = project.GetCompilationAsync(cancellationToken).GetAwaiter().GetResult();
            if (compilation is null)
            {
                throw new InvalidOperationException("Unable to get compilation from project.");
            }

            if (FileSystem != null)
            {
                compilation = compilation.WithFileSystem(FileSystem);
            }

            var analyzers = (CreateAdditionalAnalyzers() ?? Array.Empty<DiagnosticAnalyzer>())
                .Where(analyzer => analyzer.SupportedDiagnostics.Any(descriptor => diagnosticIds.Contains(descriptor.Id)))
                .ToImmutableArray();

            var diagnostics = compilation.GetDiagnostics(cancellationToken);
            if (analyzers.Length > 0)
            {
                var analyzerDiagnostics = compilation
                    .WithAnalyzers(analyzers, new AnalyzerOptions(AdditionalFiles?.ToImmutableArray() ?? ImmutableArray<AdditionalText>.Empty))
                    .GetAnalyzerDiagnosticsAsync(cancellationToken).GetAwaiter().GetResult();
                diagnostics = diagnostics.AddRange(analyzerDiagnostics);
            }

            return diagnostics.Where(d => diagnosticIds.Contains(d.Id)).ToImmutableArray();
        }

        private void TestCodeFix(Document document, string expected, string diagnosticId, IDiagnosticLocator locator, ICodeActionSelector codeActionSelector)
        {
            var diagnostics = GetDiagnostics(document, diagnosticId, locator);
            TestCodeFix(document, expected, diagnostics, locator, codeActionSelector);
        }

        private void NoCodeFix(Document document, string diagnosticId, IDiagnosticLocator locator)
        {
            var diagnostics = GetDiagnostics(document, diagnosticId, locator);
            NoCodeFix(document, diagnostics, locator);
        }

        private void NoCodeFix(Document document, ImmutableArray<Diagnostic> diagnostics, IDiagnosticLocator locator)
        {
            var codeFixes = GetCodeFixes(document, diagnostics);
            if (codeFixes.Length != 0)
            {
                throw RoslynTestKitException.UnexpectedCodeFixFound(codeFixes, locator);
            }
        }

        private ImmutableArray<Diagnostic> GetDiagnostics(Document document, string diagnosticId, IDiagnosticLocator locator)
        {
            var reportedDiagnostics = GetReportedDiagnostics(document, locator).ToArray();
            var diagnostic = reportedDiagnostics.FirstOrDefault(x => x.Id == diagnosticId);
            if (diagnostic == null)
            {
                throw RoslynTestKitException.DiagnosticNotFound(diagnosticId, locator, reportedDiagnostics);
            }

            return WithSameSpanDiagnostics(diagnostic, reportedDiagnostics);
        }

        /// <summary>
        /// Returns <paramref name="diagnostic"/> followed by every other unsuppressed reported diagnostic
        /// on exactly the same span, because the editor host hands all of them to one
        /// <see cref="CodeFixContext"/>.
        /// </summary>
        private static ImmutableArray<Diagnostic> WithSameSpanDiagnostics(Diagnostic diagnostic, IEnumerable<Diagnostic> reportedDiagnostics)
        {
            var location = diagnostic.Location;
            return new[] { diagnostic }
                .Concat(reportedDiagnostics.Where(d =>
                    !ReferenceEquals(d, diagnostic) &&
                    !d.IsSuppressed &&
                    d.Location.SourceTree == location.SourceTree &&
                    d.Location.SourceSpan == location.SourceSpan))
                .ToImmutableArray();
        }

        private void TestCodeFix(Document document, string expected, ImmutableArray<Diagnostic> diagnostics, IDiagnosticLocator locator, ICodeActionSelector codeActionSelector)
        {
            var codeFixes = GetCodeFixes(document, diagnostics);
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

        /// <summary>
        /// Builds the <see cref="CodeFixContext"/> the way the editor host does: the located diagnostic
        /// (<paramref name="diagnostics"/>[0]) plus the same-span diagnostics the provider can fix, ordered
        /// by descending severity as the editor's <c>CodeFixService</c> orders them, so a more severe
        /// sibling can come first. The sort is stable, so the located diagnostic stays first among equals.
        /// The located diagnostic is kept even when its ID is not fixable, so <c>NoCodeFix</c> still sees it.
        /// </summary>
        private ImmutableArray<CodeAction> GetCodeFixes(Document document, ImmutableArray<Diagnostic> diagnostics)
        {
            var provider = CreateProvider();
            var fixableIds = provider.FixableDiagnosticIds;
            var contextDiagnostics = diagnostics
                .Where((d, i) => i == 0 || (!fixableIds.IsDefault && fixableIds.Contains(d.Id)))
                .OrderByDescending(d => d.Severity)
                .ToImmutableArray();
            var builder = ImmutableArray.CreateBuilder<CodeAction>();
            var context = new CodeFixContext(document, diagnostics[0].Location.SourceSpan, contextDiagnostics, (a, _) => builder.Add(a), CancellationToken.None);
            provider.RegisterCodeFixesAsync(context).GetAwaiter().GetResult();
            return builder.ToImmutable();
        }

        /// <summary>
        /// Returns the reported diagnostic for <paramref name="descriptor"/> with its same-span siblings, or,
        /// when none is reported at the locator, only a diagnostic created from the descriptor.
        /// </summary>
        private ImmutableArray<Diagnostic> FindOrCreateDiagnosticForDescriptor(Document document, DiagnosticDescriptor descriptor, IDiagnosticLocator locator)
        {
            var reportedDiagnostics = GetReportedDiagnostics(document, locator).ToList();
            var diagnostic = reportedDiagnostics.FirstOrDefault(x => x.Id == descriptor.Id);
            if (diagnostic != null)
            {
                return WithSameSpanDiagnostics(diagnostic, reportedDiagnostics);
            }

            var tree = document.GetSyntaxTreeAsync(CancellationToken.None).Result;
            if (tree == null)
            {
                throw new InvalidOperationException("Unable to get syntax tree from document.");
            }
            return ImmutableArray.Create(Diagnostic.Create(descriptor, Location.Create(tree, locator.GetSpan())));
        }
    }
}
