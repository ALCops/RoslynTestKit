namespace RoslynTestKit
{
    /// <summary>
    /// Selects how <c>TestFixAll</c> verifies the result of a FixAll code action.
    /// </summary>
    /// <remarks>
    /// The AL editor host does not apply a FixAll action to its workspace. It captures the solution
    /// the fix was computed on, diffs each changed document of every <c>ApplyChangesOperation</c>
    /// against that unchanged snapshot (<c>Document.GetTextChangesAsync</c>), converts every
    /// <c>TextChange</c> to a line/character range of the original text and sends all edits of a
    /// file in one batch that the editor applies to the unchanged buffer. A FixAll result that is
    /// correct when applied to a workspace can therefore still corrupt the code in the editor,
    /// for example when the diff contains duplicated or overlapping edits.
    /// </remarks>
    public enum FixAllHostVerification
    {
        /// <summary>
        /// Only applies the operations to the test workspace and compares the resulting documents.
        /// This is the behaviour of releases before host replay was added.
        /// </summary>
        None = 0,

        /// <summary>
        /// Default. Also replays the editor host's text-edit diff: every changed document of every
        /// <c>ApplyChangesOperation</c> is diffed against the original document, the edits of all
        /// operations are collected per document and applied in one batch to the original text,
        /// and that result is compared with the expected code before the workspace apply is checked.
        /// Known SDK limitation: on <c>Microsoft.Dynamics.Nav.CodeAnalysis</c> 14.x and 15.x the syntax
        /// differ (<c>SyntaxDiffer.GetCommonEdgeLengths</c>) throws an <c>IndexOutOfRangeException</c> for
        /// a syntax-root result with a change whose new text starts or ends with line breaks where the
        /// compared segments differ. Such a document is not replayed (a note is written to the test
        /// output): it loses the replay's duplicate-insert and overlap detection entirely and is checked
        /// by workspace apply only. Any other diff failure throws a <see cref="RoslynTestKitException"/>.
        /// </summary>
        TextEdits = 1,

        /// <summary>
        /// Like <see cref="TextEdits"/>, but each changed document is diffed against a re-parsed copy
        /// of the original document (same document id, fresh <c>SourceText</c> and syntax tree).
        /// The new text is then not derived from the diff base, so the host falls back to the
        /// syntax-tree diff (<c>SyntaxTree.GetChanges</c>) instead of the exact text-change chain.
        /// This is the path the editor takes when its buffer text is not the text instance the
        /// fix was computed on.
        /// </summary>
        TextEditsOnReparsedText = 2,
    }
}
