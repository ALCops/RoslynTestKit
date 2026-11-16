using System;

namespace RoslynTestKit
{
    /// <summary>
    /// A source project added to the test workspace next to the test project. The test project
    /// gets a project reference to it, so its objects resolve as source symbols from another
    /// module, the way a multi-project workspace in the AL editor resolves them.
    /// </summary>
    public sealed class DependencyProject
    {
        public DependencyProject(string name, string code)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name cannot be empty", nameof(name));
            }

            Name = name;
            Code = code ?? throw new ArgumentNullException(nameof(code));
        }

        /// <summary>
        /// App name. Also used as the workspace project name, so it must be unique per test.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// AL source of the project. Use <c>/*EOD*/</c> to split it into several documents.
        /// </summary>
        public string Code { get; }

        public string Publisher { get; set; } = "Default Publisher";

        public Version Version { get; set; } = new Version(1, 0, 0, 0);

        /// <summary>
        /// App ID. Defaults to a new value per instance; the SDK resolves project references
        /// by this ID, so it must be non-empty and unique per test.
        /// </summary>
        public Guid AppId { get; set; } = Guid.NewGuid();
    }
}
