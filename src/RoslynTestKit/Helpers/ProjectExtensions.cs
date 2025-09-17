using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces;

namespace RoslynTestKit
{
    public static class ProjectExtensions
    {
        public static Project AddMetadataReferences(this Project project, IEnumerable<MetadataReference> metadataReferences)
        {
            // TODO: Implement logic for adding metadata references to the project
            return project;
        }
    }
}