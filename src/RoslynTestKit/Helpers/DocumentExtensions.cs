using System.Threading;
using System.Threading.Tasks;
using Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces;
using Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces.Options;

namespace RoslynTestKit
{
    public static class DocumentExtensions
    {
        public static async Task<OptionSet> GetOptionsAsync(this Document document, CancellationToken cancellationToken = default)
        {
            var text = await document.GetTextAsync(cancellationToken);
            Workspace.TryGetWorkspace(text.Container, out var workspace);
            return workspace.Options;
        }
    }
}