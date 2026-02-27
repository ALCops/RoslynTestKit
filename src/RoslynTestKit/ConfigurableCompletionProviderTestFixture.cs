using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces;
using Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces.Completion;
using AdditionalText = Microsoft.Dynamics.Nav.CodeAnalysis.AdditionalText;
using CompilationOptions = Microsoft.Dynamics.Nav.CodeAnalysis.CompilationOptions;
using ParseOptions = Microsoft.Dynamics.Nav.CodeAnalysis.ParseOptions;

namespace RoslynTestKit
{
    internal class ConfigurableCompletionProviderTestFixture: CompletionProviderFixture
    {
        private readonly CompletionProviderTestFixtureConfig _config;
        private CompletionProvider _provider;

        public ConfigurableCompletionProviderTestFixture(CompletionProvider provider, CompletionProviderTestFixtureConfig config)
        {
            _config = config;
            _provider = provider;
        }

        protected override string LanguageName => _config.Language;
        protected override CompletionProvider CreateProvider() => _provider;
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