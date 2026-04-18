using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces;
using AdditionalText = Microsoft.Dynamics.Nav.CodeAnalysis.AdditionalText;
using CompilationOptions = Microsoft.Dynamics.Nav.CodeAnalysis.CompilationOptions;
using LanguageNames = Microsoft.Dynamics.Nav.CodeAnalysis.LanguageNames;
using ParseOptions = Microsoft.Dynamics.Nav.CodeAnalysis.ParseOptions;

namespace RoslynTestKit
{
    public abstract class BaseTestFixtureConfig
    {
        public IReadOnlyList<MetadataReference> References { get; set; } = ImmutableArray<MetadataReference>.Empty;

        public bool ThrowsWhenInputDocumentContainsError { get; set; } = true;
        public string Language { get; set; } = LanguageNames.AL;

        public IReadOnlyList<AdditionalText> AdditionalFiles { get; set; } = ImmutableArray<AdditionalText>.Empty;

        /// <summary>
        /// Path to a ruleset file (.ruleset) that controls which diagnostics are enabled and at what severity.
        /// When null, the default ruleset behaviour applies.
        /// </summary>
        public string? RuleSetPath { get; set; } = null;

        /// <summary>
        /// Paths to directories that contain .app packages the compiler should resolve symbols from.
        /// </summary>
        public IReadOnlyList<string> PackageCachePaths { get; set; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// Override the default <see cref="CompilationOptions"/> used when compiling the test project.
        /// When null, <c>new CompilationOptions()</c> is used.
        /// </summary>
        public CompilationOptions? CompilationOptions { get; set; } = null;

        /// <summary>
        /// Override the default <see cref="ParseOptions"/> used when parsing test documents.
        /// When null, the platform default applies.
        /// </summary>
        public ParseOptions? ParseOptions { get; set; } = null;

        /// <summary>
        /// Optional callback applied to the <see cref="ProjectInfo"/> after all other settings have been
        /// applied. Use this escape hatch to set any <see cref="ProjectInfo"/> property not exposed
        /// directly on the config.
        /// </summary>
        public Func<ProjectInfo, ProjectInfo>? ProjectInfoCustomizer { get; set; } = null;

        /// <summary>
        /// Optional <see cref="IFileSystem"/> injected into the <see cref="Compilation"/> before analyzers
        /// run. Use the SDK's <see cref="MemoryFileSystem"/> to provide in-memory files (e.g. XLIFF
        /// translation files) that analyzers read via <c>Compilation.FileSystem</c>.
        /// When null, the compilation keeps whatever file system the workspace assigns (typically none).
        /// </summary>
        public IFileSystem? FileSystem { get; set; } = null;
    }
}