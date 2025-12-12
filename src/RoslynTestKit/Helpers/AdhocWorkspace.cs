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
        private static MethodInfo? _cachedCreateMethod;
        private static ParameterInfo[]? _cachedParameters;
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
            // There's a binary compatibility issue caused by a breaking change in the ProjectInfo.Create method signature between version v17.0.28.6483 and v17.0.28.26016 of Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces.dll.
            // hence we use reflection to call the method in a way that works for both versions
            var info = CreateProjectInfoViaReflection(name, language);
            return AddProject(info);
        }

        /// <summary>
        /// Creates a ProjectInfo instance using reflection to handle different versions
        /// of Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces that have different method signatures.
        /// </summary>
        private static ProjectInfo CreateProjectInfoViaReflection(string name, string language)
        {
            if (_cachedCreateMethod == null)
            {
                // Find the Create method - there should be only one static Create method
                _cachedCreateMethod = typeof(ProjectInfo)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Create")
                    ?? throw new InvalidOperationException("Could not find ProjectInfo.Create method via reflection.");

                _cachedParameters = _cachedCreateMethod.GetParameters();
            }

            var parameters = _cachedParameters!;
            var args = new object?[parameters.Length];

            // The first 5 parameters are always: id, version, name, assemblyName, language (required)
            // All other parameters are optional - use their default values via ParameterInfo.DefaultValue
            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var paramName = param.Name?.ToLowerInvariant();

                // Handle the 5 required parameters explicitly
                if (paramName == "id")
                {
                    args[i] = ProjectId.CreateNewId();
                }
                else if (paramName == "version")
                {
                    args[i] = VersionStamp.Create();
                }
                else if (paramName == "name")
                {
                    args[i] = name;
                }
                else if (paramName == "assemblyname")
                {
                    args[i] = name;
                }
                else if (paramName == "language")
                {
                    args[i] = language;
                }
                // For optional parameters, use their default value or appropriate fallback
                else if (param.HasDefaultValue)
                {
                    args[i] = param.DefaultValue;
                }
                else if (!param.ParameterType.IsValueType)
                {
                    // Reference types without default - use null
                    args[i] = null;
                }
                else
                {
                    // Value types without default - use default(T)
                    args[i] = Activator.CreateInstance(param.ParameterType);
                }
            }

            var result = _cachedCreateMethod.Invoke(null, args);
            if (result is not ProjectInfo projectInfo)
            {
                throw new InvalidOperationException("ProjectInfo.Create did not return a ProjectInfo instance.");
            }

            return projectInfo;
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