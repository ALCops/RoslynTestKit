using System;
using System.Collections.Generic;
#if NET8_0_OR_GREATER
using System.Linq;
using System.Reflection;
#endif
using System.Threading;
using Microsoft.Dynamics.Nav.CodeAnalysis.Text;
using Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces;
using Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces.Host;

namespace RoslynTestKit
{
    /// <summary>
    /// A workspace that allows full manipulation of projects and documents,
    /// but does not persist changes.
    /// </summary>
    public sealed class AdhocWorkspace : Workspace
    {
#if NET8_0_OR_GREATER
        private static readonly object _reflectionLock = new object();
        private static (MethodInfo Method, ParameterInfo[] Parameters)? _cachedCreateMethodInfo;
#endif

        public AdhocWorkspace(HostServices host, string workspaceKind = "Custom")
            : base(host, workspaceKind)
        {
        }

        public AdhocWorkspace()
            : this(HostServices.DefaultHost)
        {
        }

        public override bool CanApplyChange(ApplyChangesKind feature)
        {
            // all kinds supported.
            return true;
        }

        /// <summary>
        /// Returns true, signifiying that you can call the open and close document APIs to add the document into the open document list.
        /// </summary>
        public override bool CanOpenDocuments => true;

        /// <summary>
        /// Clears all projects and documents from the workspace.
        /// </summary>
        public new void ClearSolution()
            => base.ClearSolution();

        /// <summary>
        /// Adds an entire solution to the workspace, replacing any existing solution.
        /// </summary>
        public Solution AddSolution(SolutionInfo solutionInfo)
        {
            if (solutionInfo is null)
            {
                throw new ArgumentNullException(nameof(solutionInfo));
            }

            OnSolutionAdded(solutionInfo);

            //this.UpdateReferencesAfterAdd();

            return CurrentSolution;
        }

        /// <summary>
        /// Adds a project to the workspace. All previous projects remain intact.
        /// </summary>
#if NETSTANDARD2_1
        public Project? AddProject(string name, string language)
        {
            var info = ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(), name, name, language);
            return AddProject(info);
        }
#endif

#if NET8_0_OR_GREATER
        public Project? AddProject(string name, string language)
        {
            var info = CreateProjectInfoViaReflection(name, language);
            return AddProject(info);
        }

        /// <summary>
        /// Creates a ProjectInfo instance using reflection to handle different versions
        /// of Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces that have different method signatures.
        /// </summary>
        /// <remarks>
        /// This method is necessary because the ProjectInfo.Create method signature changed between
        /// version v17.0.28.6483 and v17.0.28.26016 of Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces.dll.
        /// By using reflection, we can call the method regardless of which version is loaded at runtime.
        /// </remarks>
        private static ProjectInfo CreateProjectInfoViaReflection(string name, string language)
        {
            var (method, parameters) = GetCachedCreateMethod();
            var args = BuildMethodArguments(parameters, name, language);

            var result = method.Invoke(null, args);
            if (result is not ProjectInfo projectInfo)
            {
                throw new InvalidOperationException("ProjectInfo.Create did not return a ProjectInfo instance.");
            }

            return projectInfo;
        }

        /// <summary>
        /// Gets the cached MethodInfo and ParameterInfo for ProjectInfo.Create, initializing if necessary.
        /// Thread-safe using double-checked locking pattern.
        /// </summary>
        private static (MethodInfo Method, ParameterInfo[] Parameters) GetCachedCreateMethod()
        {
            var cached = _cachedCreateMethodInfo;
            if (cached != null)
            {
                return cached.Value;
            }

            lock (_reflectionLock)
            {
                cached = _cachedCreateMethodInfo;
                if (cached != null)
                {
                    return cached.Value;
                }

                var createMethod = typeof(ProjectInfo)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Create")
                    ?? throw new InvalidOperationException(
                        "Could not find ProjectInfo.Create method. Ensure Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces is referenced.");

                var result = (createMethod, createMethod.GetParameters());
                _cachedCreateMethodInfo = result;
                return result;
            }
        }

        /// <summary>
        /// Builds the argument array for calling ProjectInfo.Create via reflection.
        /// </summary>
        private static object?[] BuildMethodArguments(ParameterInfo[] parameters, string name, string language)
        {
            var args = new object?[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                args[i] = GetParameterValue(parameters[i], name, language);
            }

            return args;
        }

        /// <summary>
        /// Determines the value to pass for a given parameter of ProjectInfo.Create.
        /// </summary>
        private static object? GetParameterValue(ParameterInfo parameter, string name, string language)
        {
            var paramName = parameter.Name?.ToLowerInvariant() ?? string.Empty;

            // Handle the required parameters (first 5 in the method signature)
            return paramName switch
            {
                "id" => ProjectId.CreateNewId(),
                "version" => VersionStamp.Create(),
                "name" => name,
                "assemblyname" => name,
                "language" => language,
                // For optional parameters, use their declared default value or an appropriate fallback
                _ => GetDefaultParameterValue(parameter)
            };
        }

        /// <summary>
        /// Gets the default value for an optional parameter.
        /// </summary>
        private static object? GetDefaultParameterValue(ParameterInfo parameter)
        {
            if (parameter.HasDefaultValue)
            {
                return parameter.DefaultValue;
            }

            // Fallback for parameters without explicit defaults
            return parameter.ParameterType.IsValueType
                ? Activator.CreateInstance(parameter.ParameterType)
                : null;
        }
#endif

        /// <summary>
        /// Adds a project to the workspace. All previous projects remain intact.
        /// </summary>
        public Project? AddProject(ProjectInfo projectInfo)
        {
            if (projectInfo is null)
            {
                throw new ArgumentNullException(nameof(projectInfo));
            }

            OnProjectAdded(projectInfo);

            // this.UpdateReferencesAfterAdd(); // does not contain a definition for 'UpdateReferencesAfterAdd'

            return CurrentSolution.GetProject(projectInfo.Id);
        }

        /// <summary>
        /// Adds multiple projects to the workspace at once. All existing projects remain intact.
        /// </summary>
        /// <param name="projectInfos"></param>
        public void AddProjects(IEnumerable<ProjectInfo> projectInfos)
        {
            if (projectInfos is null)
            {
                throw new ArgumentNullException(nameof(projectInfos));
            }

            foreach (var info in projectInfos)
            {
                OnProjectAdded(info);
            }

            //this.UpdateReferencesAfterAdd(); // does not contain a definition for 'UpdateReferencesAfterAdd'
        }

        /// <summary>
        /// Adds a document to the workspace.
        /// </summary>
        public Document? AddDocument(ProjectId projectId, string name, SourceText text)
        {
            if (projectId is null)
            {
                throw new ArgumentNullException(nameof(projectId));
            }

            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var id = DocumentId.CreateNewId(projectId);
            var loader = TextLoader.From(TextAndVersion.Create(text, VersionStamp.Create()));

            return AddDocument(DocumentInfo.Create(id, name, loader: loader));
        }

        /// <summary>
        /// Adds a document to the workspace.
        /// </summary>
        public Document? AddDocument(DocumentInfo documentInfo)
        {
            if (documentInfo is null)
            {
                throw new ArgumentNullException(nameof(documentInfo));
            }

            OnDocumentAdded(documentInfo);

            return CurrentSolution.GetDocument(documentInfo.Id);
        }

        /// <summary>
        /// Puts the specified document into the open state.
        /// </summary>
        public override void OpenDocument(DocumentId documentId, bool activate = true)
        {
            var doc = CurrentSolution.GetDocument(documentId);
            if (doc != null)
            {
                var text = doc.GetTextAsync(CancellationToken.None).Result;
                OnDocumentOpened(documentId, text.Container, activate);
            }
        }

        /// <summary>
        /// Puts the specified document into the closed state.
        /// </summary>
        public override void CloseDocument(DocumentId documentId)
        {
            var doc = CurrentSolution.GetDocument(documentId);
            if (doc != null)
            {
                var text = doc.GetTextAsync(CancellationToken.None).Result;
                var version = doc.GetTextVersionAsync(CancellationToken.None).Result;
                var loader = TextLoader.From(TextAndVersion.Create(text, version, doc.FilePath));
                OnDocumentClosed(documentId, loader);
            }
        }

        /// <summary>
        /// Puts the specified additional document into the open state.
        /// </summary>
        public override void OpenAdditionalDocument(DocumentId documentId, bool activate = true)
        {
            var doc = CurrentSolution.GetAdditionalDocument(documentId);
            if (doc != null)
            {
                var text = doc.GetTextAsync(CancellationToken.None).Result;
                OnAdditionalDocumentOpened(documentId, text.Container, activate);
            }
        }

        /// <summary>
        /// Puts the specified additional document into the closed state
        /// </summary>
        public override void CloseAdditionalDocument(DocumentId documentId)
        {
            var doc = CurrentSolution.GetAdditionalDocument(documentId);
            if (doc != null)
            {
                var text = doc.GetTextAsync(CancellationToken.None).Result;
                var version = doc.GetTextVersionAsync(CancellationToken.None).Result;
                var loader = TextLoader.From(TextAndVersion.Create(text, version, doc.FilePath));
                OnAdditionalDocumentClosed(documentId, loader);
            }
        }

        /// <summary>
        /// Puts the specified analyzer config document into the open state.
        /// </summary>
        //public override void OpenAnalyzerConfigDocument(DocumentId documentId, bool activate = true)
        //{
        //    var doc = this.CurrentSolution.GetAnalyzerConfigDocument(documentId);
        //    if (doc != null)
        //    {
        //        var text = doc.GetTextSynchronously(CancellationToken.None);
        //        this.OnAnalyzerConfigDocumentOpened(documentId, text.Container, activate);
        //    }
        //}

        /// <summary>
        /// Puts the specified analyzer config document into the closed state
        /// </summary>
        //public override void CloseAnalyzerConfigDocument(DocumentId documentId)
        //{
        //    var doc = this.CurrentSolution.GetAnalyzerConfigDocument(documentId);
        //    if (doc != null)
        //    {
        //        var text = doc.GetTextSynchronously(CancellationToken.None);
        //        var version = doc.GetTextVersionSynchronously(CancellationToken.None);
        //        var loader = TextLoader.From(TextAndVersion.Create(text, version, doc.FilePath));
        //        this.OnAnalyzerConfigDocumentClosed(documentId, loader);
        //    }
        //}
    }
}