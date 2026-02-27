using System;
using System.Collections.Generic;
using Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces;
using CompilationOptions = Microsoft.Dynamics.Nav.CodeAnalysis.CompilationOptions;
using ParseOptions = Microsoft.Dynamics.Nav.CodeAnalysis.ParseOptions;

namespace RoslynTestKit
{
    /// <summary>
    /// Carries <see cref="ProjectInfo"/>-level settings from the fixture config down to
    /// <see cref="AdhocWorkspace.AddProject(string,string,ProjectSettings)"/>.
    /// Kept internal so that it does not become part of the public API surface.
    /// </summary>
    internal sealed class ProjectSettings
    {
        public string? RuleSetPath { get; set; }
        public IReadOnlyList<string>? PackageCachePaths { get; set; }
        public CompilationOptions? CompilationOptions { get; set; }
        public ParseOptions? ParseOptions { get; set; }
        public Func<ProjectInfo, ProjectInfo>? ProjectInfoCustomizer { get; set; }
    }
}
