using System;

namespace RoslynTestKit
{
    public class TransformedCodeDifferentThanExpectedException: RoslynTestKitException
    {
        public string Diff { get; }
        public string TransformedCode { get; }
        public string ExpectedCode { get; }

        public TransformedCodeDifferentThanExpectedException(string transformedCode, string expectedCode, string diff)
            : this(transformedCode, expectedCode, diff, stage: null)
        {
        }

        /// <param name="stage">Names the verification step that produced <paramref name="transformedCode"/>
        /// when a result is verified in more than one way; <c>null</c> keeps the plain message.</param>
        internal TransformedCodeDifferentThanExpectedException(string transformedCode, string expectedCode, string diff, string? stage)
            : base(stage is null
                ? $"Transformed code is different than expected:{Environment.NewLine}{diff}"
                : $"Transformed code is different than expected ({stage}):{Environment.NewLine}{diff}")
        {
            Diff = diff;
            TransformedCode = transformedCode;
            ExpectedCode = expectedCode;
        }
    }
}