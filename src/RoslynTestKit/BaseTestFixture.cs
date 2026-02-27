using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces;
using AdditionalText = Microsoft.Dynamics.Nav.CodeAnalysis.AdditionalText;
using CompilationOptions = Microsoft.Dynamics.Nav.CodeAnalysis.CompilationOptions;
using Document = Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces.Document;
using LanguageNames = Microsoft.Dynamics.Nav.CodeAnalysis.LanguageNames;
using ParseOptions = Microsoft.Dynamics.Nav.CodeAnalysis.ParseOptions;

namespace RoslynTestKit
{
    public abstract class BaseTestFixture
    {
        protected abstract string LanguageName { get; }

        protected virtual bool ThrowsWhenInputDocumentContainsError { get; } = false;

        protected virtual IReadOnlyCollection<MetadataReference>? References => null;

        protected virtual IReadOnlyCollection<AdditionalText>? AdditionalFiles => null;

        /// <summary>
        /// Path to a ruleset file (.ruleset) applied to the test project.
        /// When null, the default ruleset behaviour applies.
        /// </summary>
        protected virtual string? RuleSetPath => null;

        /// <summary>
        /// Package cache paths made available to the compiler when resolving symbols.
        /// </summary>
        protected virtual IReadOnlyList<string>? PackageCachePaths => null;

        /// <summary>
        /// Override the <see cref="CompilationOptions"/> used when compiling the test project.
        /// When null, <c>new CompilationOptions()</c> is used.
        /// </summary>
        protected virtual CompilationOptions? CustomCompilationOptions => null;

        /// <summary>
        /// Override the <see cref="ParseOptions"/> applied to the test project.
        /// When null, the platform default applies.
        /// </summary>
        protected virtual ParseOptions? ParseOptions => null;

        /// <summary>
        /// Optional callback applied to the <see cref="ProjectInfo"/> after all other settings have
        /// been applied. Use this escape hatch to configure any <see cref="ProjectInfo"/> property
        /// not directly exposed on the fixture.
        /// </summary>
        protected virtual Func<ProjectInfo, ProjectInfo>? ProjectInfoCustomizer => null;

        protected Document CreateDocumentFromCode(string code)
        {
            return CreateDocumentFromCode(code, LanguageName, References ?? Array.Empty<MetadataReference>());
        }

        internal const string FileSeparator = "/*EOD*/";
        private readonly static Regex FileSeparatorPattern = new Regex(Regex.Escape(FileSeparator));

        /// <summary>
        /// Should create the compilation and return a document that represents the provided code
        /// </summary>
        protected virtual Document CreateDocumentFromCode(string code, string languageName, IReadOnlyCollection<MetadataReference> extraReferences)
        {
            var frameworkReferences = CreateFrameworkMetadataReferences();

            var compilationOptions = CustomCompilationOptions ?? GetCompilationOptions(languageName);

            var settings = new ProjectSettings
            {
                RuleSetPath = RuleSetPath,
                PackageCachePaths = PackageCachePaths,
                ParseOptions = ParseOptions,
                ProjectInfoCustomizer = ProjectInfoCustomizer
            };

            var docs = FileSeparatorPattern.Split(code).Reverse().ToList();
            if (docs.Count == 0)
            {
                throw new ArgumentException("Code cannot be empty after splitting", nameof(code));
            }

            var project = new AdhocWorkspace()
                .AddProject("TestProject", languageName, settings)
                .WithCompilationOptions(compilationOptions)
                .AddMetadataReferences(frameworkReferences)
                .AddMetadataReferences(extraReferences);

            Document? mainDocument = null;
            foreach (var doc in docs.Select((e, i) => (e, i)))
            {
                var docContent = docs.Count > 1 ? doc.e.Trim() : doc.e;
                mainDocument = project.AddDocument($"TestDocument{doc.i}", docContent);
                project = mainDocument.Project;
            }

            return mainDocument!; // Non-null assertion since we checked docs.Count > 0
        }

        private static CompilationOptions GetCompilationOptions(string languageName) =>
            languageName switch
            {
                // LanguageNames.CSharp => new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                // LanguageNames.VisualBasic => new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                LanguageNames.AL => new CompilationOptions(),
                _ => throw new NotSupportedException($"Language {languageName} is not supported")
            };

        protected virtual IEnumerable<MetadataReference> CreateFrameworkMetadataReferences()
        {
            yield return ReferenceSource.Core;
            yield return ReferenceSource.Linq;
            yield return ReferenceSource.LinqExpression;

            if (ReferenceSource.Core?.Display?.EndsWith("mscorlib.dll") == false)
            {
                foreach (var netStandardCoreLib in ReferenceSource.NetStandardBasicLibs.Value)
                {
                    yield return netStandardCoreLib;
                }
            }
        }
    }
}
