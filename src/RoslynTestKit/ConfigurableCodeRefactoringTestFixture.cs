using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.CodeRefactoring;
using Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces;
using AdditionalText = Microsoft.Dynamics.Nav.CodeAnalysis.AdditionalText;
using CompilationOptions = Microsoft.Dynamics.Nav.CodeAnalysis.CompilationOptions;
using ParseOptions = Microsoft.Dynamics.Nav.CodeAnalysis.ParseOptions;

namespace RoslynTestKit
{
    internal class ConfigurableCodeRefactoringTestFixture: CodeRefactoringTestFixture
    {
        private readonly CodeRefactoringTestFixtureConfig _config;
        private CodeRefactoringProvider _provider;

        public ConfigurableCodeRefactoringTestFixture(CodeRefactoringProvider provider, CodeRefactoringTestFixtureConfig config)
        {
            _config = config;
            _provider = provider;
        }

        protected override string LanguageName => _config.Language;
        protected override CodeRefactoringProvider CreateProvider() => _provider;
        protected override IReadOnlyCollection<MetadataReference> References => _config.References;
        protected override IReadOnlyCollection<AdditionalText> AdditionalFiles => _config.AdditionalFiles;
        protected override bool ThrowsWhenInputDocumentContainsError => _config.ThrowsWhenInputDocumentContainsError;
        protected override string? RuleSetPath => _config.RuleSetPath;
        protected override IReadOnlyList<string>? PackageCachePaths => _config.PackageCachePaths;
        protected override CompilationOptions? CustomCompilationOptions => _config.CompilationOptions;
        protected override ParseOptions? ParseOptions => _config.ParseOptions;
        protected override Func<ProjectInfo, ProjectInfo>? ProjectInfoCustomizer => _config.ProjectInfoCustomizer;
        protected override IFileSystem? FileSystem => _config.FileSystem;
    }
}