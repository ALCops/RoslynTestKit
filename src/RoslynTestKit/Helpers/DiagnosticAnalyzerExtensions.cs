using System.Collections.Immutable;
using System.Threading;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Diagnostics;

namespace RoslynTestKit
{
    public static class DiagnosticAnalyzerExtensions
    {
        /// <summary>
        /// Returns a new compilation with attached diagnostic analyzers.
        /// </summary>
        /// <param name="compilation">Compilation to which analyzers are to be added.</param>
        /// <param name="analyzers">The set of analyzers to include in future analyses.</param>
        /// <param name="options">Options that are passed to analyzers.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to abort analysis.</param>
        public static CompilationWithAnalyzers WithAnalyzers(this Compilation compilation, ImmutableArray<DiagnosticAnalyzer> analyzers, AnalyzerOptions? options = null, CancellationToken cancellationToken = default)
        {
            return new CompilationWithAnalyzers(compilation, analyzers, options ?? new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty), cancellationToken);
        }
    }
}