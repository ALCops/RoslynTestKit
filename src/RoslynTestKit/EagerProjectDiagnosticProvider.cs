using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dynamics.Nav.CodeAnalysis.CodeFixes;
using Microsoft.Dynamics.Nav.CodeAnalysis.Diagnostics;
using Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces;
using Document = Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces.Document;

namespace RoslynTestKit
{
    /// <summary>
    /// FixAll diagnostic provider for <see cref="FixAllScope.Project"/> and
    /// <see cref="FixAllScope.Workspace"/> scope that behaves like the editor host's eager provider:
    /// every call recomputes the diagnostics from the compilation of the project it is given,
    /// instead of returning a list computed before the FixAll started.
    /// </summary>
    internal sealed class EagerProjectDiagnosticProvider : FixAllContext.DiagnosticProvider
    {
        private readonly Func<Project, ISet<string>, CancellationToken, ImmutableArray<Diagnostic>> _getProjectDiagnostics;

        public EagerProjectDiagnosticProvider(Func<Project, ISet<string>, CancellationToken, ImmutableArray<Diagnostic>> getProjectDiagnostics)
        {
            _getProjectDiagnostics = getProjectDiagnostics;
        }

        /// <remarks>
        /// The host filters by file path. Documents of an in-memory test workspace have no file path,
        /// so the filter compares syntax-tree identity instead, which selects the same diagnostics.
        /// </remarks>
        public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(
            Document document, ISet<string> diagnosticIdsWithFixes, CancellationToken cancellationToken)
        {
            var tree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
            return _getProjectDiagnostics(document.Project, diagnosticIdsWithFixes, cancellationToken)
                .Where(d => d.Location.IsInSource && d.Location.SourceTree == tree);
        }

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(
            Project project, ISet<string> diagnosticIdsWithFixes, CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<Diagnostic>>(_getProjectDiagnostics(project, diagnosticIdsWithFixes, cancellationToken));
        }

        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(
            Project project, ISet<string> diagnosticIdsWithFixes, CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<Diagnostic>>(_getProjectDiagnostics(project, diagnosticIdsWithFixes, cancellationToken));
        }
    }
}
