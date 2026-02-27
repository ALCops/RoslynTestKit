using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Diagnostics;
using Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces;
using AdditionalText = Microsoft.Dynamics.Nav.CodeAnalysis.AdditionalText;
using CompilationOptions = Microsoft.Dynamics.Nav.CodeAnalysis.CompilationOptions;
using ParseOptions = Microsoft.Dynamics.Nav.CodeAnalysis.ParseOptions;

namespace RoslynTestKit
{
    internal class ConfigurableAnalyzerTestFixture: AnalyzerTestFixture
    {
        private readonly DiagnosticAnalyzer _analyzer;
        private readonly AnalyzerTestFixtureConfig _config;

        public ConfigurableAnalyzerTestFixture(DiagnosticAnalyzer diagnosticAnalyzer, AnalyzerTestFixtureConfig config)
        {
            _analyzer = diagnosticAnalyzer;
            _config = config;
        }

        protected override string LanguageName => _config.Language;
        protected override DiagnosticAnalyzer CreateAnalyzer() => _analyzer;
        protected override IReadOnlyCollection<MetadataReference> References => _config.References;
        protected override IReadOnlyCollection<AdditionalText> AdditionalFiles => _config.AdditionalFiles;
        protected override bool ThrowsWhenInputDocumentContainsError => _config.ThrowsWhenInputDocumentContainsError;
        protected override string? RuleSetPath => _config.RuleSetPath;
        protected override IReadOnlyList<string>? PackageCachePaths => _config.PackageCachePaths;
        protected override CompilationOptions? CustomCompilationOptions => _config.CompilationOptions;
        protected override ParseOptions? ParseOptions => _config.ParseOptions;
        protected override Func<ProjectInfo, ProjectInfo>? ProjectInfoCustomizer => _config.ProjectInfoCustomizer;
    }
}