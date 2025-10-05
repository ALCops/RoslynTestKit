using System;
using System.Linq;
using System.Reflection;
using Microsoft.Dynamics.Nav.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace RoslynTestKit
{
    /// <summary>
    /// NUnit base class that detects the installed Microsoft.Dynamics.Nav.CodeAnalysis (AL DevTools)
    /// assembly version and provides helpers to conditionally run or skip analyzer tests based on
    /// version constraints.
    /// </summary>
    public abstract class NavCodeAnalysisBase
    {
        #region Fields

        protected static Version? _navCodeAnalysisVersion;
        private static readonly Assembly? _navCodeAnalysisAssembly = Assembly.GetAssembly(typeof(DiagnosticAnalyzer));

        #endregion

        #region Initialization

        [OneTimeSetUp]
        public void BaseSetUp()
        {
            RetrieveNavCodeAnalysisVersion();
        }

        /// <summary>
        /// Retrieves the AssemblyFileVersionAttribute from Microsoft.Dynamics.Nav.CodeAnalysis assembly
        /// </summary>
        private static void RetrieveNavCodeAnalysisVersion()
        {
            var navCodeAnalysisAssembly = _navCodeAnalysisAssembly;
            if (navCodeAnalysisAssembly == null)
            {
                throw new InvalidOperationException("Unable to locate Microsoft.Dynamics.Nav.CodeAnalysis assembly.");
            }

            var fileVersionAttribute = navCodeAnalysisAssembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
            if (fileVersionAttribute == null)
            {
                throw new InvalidOperationException("AssemblyFileVersionAttribute not found in Microsoft.Dynamics.Nav.CodeAnalysis assembly.");
            }

            if (string.IsNullOrEmpty(fileVersionAttribute.Version))
            {
                throw new InvalidOperationException("AssemblyFileVersionAttribute.Version is null or empty in Microsoft.Dynamics.Nav.CodeAnalysis assembly.");
            }

            if (!Version.TryParse(fileVersionAttribute.Version, out var parsedVersion))
            {
                throw new InvalidOperationException($"Unable to parse AssemblyFileVersionAttribute.Version '{fileVersionAttribute.Version}'.");
            }

            _navCodeAnalysisVersion = parsedVersion;

            TestContext.WriteLine($"Microsoft.Dynamics.Nav.CodeAnalysis version: {_navCodeAnalysisVersion}");
        }

        #endregion

        #region Version Information Getters

        /// <summary>
        /// Gets the current Nav.CodeAnalysis version as a parsed Version object
        /// </summary>
        protected static Version? GetNavCodeAnalysisParsed() => _navCodeAnalysisVersion;

        /// <summary>
        /// Checks if the Nav.CodeAnalysis version was successfully detected
        /// </summary>
        /// <returns>True if version information is available, false otherwise</returns>
        protected static bool IsVersionDetected() => _navCodeAnalysisVersion != null;

        #endregion

        #region Version Comparison Methods

        private static Version? TryParseVersion(string version)
        {
            return Version.TryParse(version, out var parsedVersion) ? parsedVersion : null;
        }

        /// <summary>
        /// Checks if the current Nav.CodeAnalysis version is greater than or equal to the specified version
        /// </summary>
        /// <param name="version">The version to compare against (e.g., "15.0.20")</param>
        /// <returns>True if current version is greater than or equal to the specified version</returns>
        /// <example>
        /// Use when you need features introduced in a specific version or later:
        /// <code>if (IsVersionGreaterOrEqual("15.0.20")) { /* use new feature */ }</code>
        /// </example>
        protected static bool IsVersionGreaterOrEqual(string version)
        {
            var compareVersion = TryParseVersion(version);
            if (compareVersion == null || _navCodeAnalysisVersion == null)
            {
                return false;
            }

            return _navCodeAnalysisVersion >= compareVersion;
        }

        /// <summary>
        /// Checks if the current Nav.CodeAnalysis version is less than the specified version
        /// </summary>
        /// <param name="version">The version to compare against (e.g., "16.0.0")</param>
        /// <returns>True if current version is less than the specified version</returns>
        /// <example>
        /// Use when excluding tests for newer versions that don't support legacy features:
        /// <code>if (IsVersionLessThan("16.0.0")) { /* test legacy behavior */ }</code>
        /// </example>
        protected static bool IsVersionLessThan(string version)
        {
            var compareVersion = TryParseVersion(version);
            if (compareVersion == null || _navCodeAnalysisVersion == null)
            {
                return false;
            }

            return _navCodeAnalysisVersion < compareVersion;
        }

        /// <summary>
        /// Checks if the current Nav.CodeAnalysis version is greater than the specified version
        /// </summary>
        /// <param name="version">The version to compare against (e.g., "15.0.19")</param>
        /// <returns>True if current version is greater than the specified version</returns>
        /// <example>
        /// Use when you need versions newer than a specific version (excluding that version):
        /// <code>if (IsVersionGreaterThan("15.0.19")) { /* requires newer than 15.0.19 */ }</code>
        /// </example>
        protected static bool IsVersionGreaterThan(string version)
        {
            var compareVersion = TryParseVersion(version);
            if (compareVersion == null || _navCodeAnalysisVersion == null)
            {
                return false;
            }

            return _navCodeAnalysisVersion > compareVersion;
        }

        /// <summary>
        /// Checks if the current Nav.CodeAnalysis version is less than or equal to the specified version
        /// </summary>
        /// <param name="version">The version to compare against (e.g., "15.0.20")</param>
        /// <returns>True if current version is less than or equal to the specified version</returns>
        /// <example>
        /// Use when testing features that were deprecated or changed after a specific version:
        /// <code>if (IsVersionLessOrEqual("15.0.20")) { /* test behavior up to and including 15.0.20 */ }</code>
        /// </example>
        protected static bool IsVersionLessOrEqual(string version)
        {
            var compareVersion = TryParseVersion(version);
            if (compareVersion == null || _navCodeAnalysisVersion == null)
            {
                return false;
            }

            return _navCodeAnalysisVersion <= compareVersion;
        }

        /// <summary>
        /// Checks if the current Nav.CodeAnalysis version matches a specific version pattern
        /// </summary>
        /// <param name="majorVersion">Major version to match</param>
        /// <param name="minorVersion">Minor version to match (optional)</param>
        /// <returns>True if the version matches the pattern</returns>
        protected static bool IsVersion(int majorVersion, int? minorVersion = null)
        {
            if (_navCodeAnalysisVersion == null)
            {
                return false;
            }

            if (minorVersion.HasValue)
            {
                return _navCodeAnalysisVersion.Major == majorVersion &&
                       _navCodeAnalysisVersion.Minor == minorVersion.Value;
            }

            return _navCodeAnalysisVersion.Major == majorVersion;
        }

        #endregion

        #region Test Helper Methods - Test Case Skipping

        /// <summary>
        /// Skips test cases that require a minimum version if the current version doesn't meet the requirement
        /// </summary>
        /// <param name="testCases">Array of test case names that require the minimum version</param>
        /// <param name="currentTestCase">The current test case being executed</param>
        /// <param name="minimumVersion">The minimum required version (e.g., "15.0.0")</param>
        /// <param name="reason">Optional custom reason for skipping (if null, a default message will be used)</param>
        /// <example>
        /// Use in test methods to skip version-specific test cases:
        /// <code>
        /// SkipTestIfVersionIsTooLow(
        ///     ["TestCase1", "TestCase2"], 
        ///     currentTestCase, 
        ///     "15.0.0", 
        ///     "Feature requires obsolete table support"
        /// );
        /// </code>
        /// </example>
        protected static void SkipTestIfVersionIsTooLow(string[] testCases, string currentTestCase, string minimumVersion, string? reason = null)
        {
            if (testCases.Contains(currentTestCase) && !IsVersionGreaterOrEqual(minimumVersion))
            {
                var message = reason ?? $"Test case requires AL version {minimumVersion} or higher.";
                Assert.Ignore(message);
            }
        }

        /// <summary>
        /// Skips test cases that require a maximum version if the current version exceeds the requirement
        /// </summary>
        /// <param name="testCases">Array of test case names that have a maximum version</param>
        /// <param name="currentTestCase">The current test case being executed</param>
        /// <param name="maximumVersion">The maximum supported version (e.g., "16.0.0")</param>
        /// <param name="reason">Optional custom reason for skipping (if null, a default message will be used)</param>
        /// <example>
        /// Use in test methods to skip test cases for versions that are too high:
        /// <code>
        /// SkipTestIfVersionIsTooHigh(
        ///     ["LegacyTestCase1", "LegacyTestCase2"], 
        ///     currentTestCase, 
        ///     "16.0.0", 
        ///     "Feature was removed in version 16.0.0"
        /// );
        /// </code>
        /// </example>
        protected static void SkipTestIfVersionIsTooHigh(string[] testCases, string currentTestCase, string maximumVersion, string? reason = null)
        {
            if (testCases.Contains(currentTestCase) && !IsVersionLessOrEqual(maximumVersion))
            {
                var message = reason ?? $"Test case requires AL version {maximumVersion} or lower.";
                Assert.Ignore(message);
            }
        }

        /// <summary>
        /// Skips test cases that require a specific version range if the current version is outside that range
        /// </summary>
        /// <param name="testCases">Array of test case names that require the version range</param>
        /// <param name="currentTestCase">The current test case being executed</param>
        /// <param name="minimumVersion">The minimum required version (inclusive, e.g., "15.0.0")</param>
        /// <param name="maximumVersion">The maximum supported version (inclusive, e.g., "16.5.0")</param>
        /// <param name="reason">Optional custom reason for skipping (if null, a default message will be used)</param>
        /// <example>
        /// Use in test methods to skip test cases that only apply to a specific version range:
        /// <code>
        /// SkipTestIfVersionOutsideRange(
        ///     ["SpecificFeatureTest"], 
        ///     currentTestCase, 
        ///     "15.0.0", 
        ///     "16.5.0",
        ///     "Feature only exists in versions 15.0.0 to 16.5.0"
        /// );
        /// </code>
        /// </example>
        protected static void SkipTestIfVersionOutsideRange(string[] testCases, string currentTestCase, string minimumVersion, string maximumVersion, string? reason = null)
        {
            if (testCases.Contains(currentTestCase))
            {
                var inRange = IsVersionGreaterOrEqual(minimumVersion) && IsVersionLessOrEqual(maximumVersion);
                if (!inRange)
                {
                    var message = reason ?? $"Test case requires AL version between {minimumVersion} and {maximumVersion}.";
                    Assert.Ignore(message);
                }
            }
        }

        #endregion

        #region Test Helper Methods - Whole Test Requirements

        /// <summary>
        /// Skips the current test if the minimum version requirement is not met
        /// </summary>
        /// <param name="minimumVersion">The minimum required version (e.g., "15.0.0")</param>
        /// <param name="reason">Optional custom reason for skipping (if null, a default message will be used)</param>
        /// <example>
        /// Use at the beginning of a test method to skip if version is too low:
        /// <code>
        /// [Test]
        /// public void TestNewFeature()
        /// {
        ///     RequireMinimumVersion("15.0.0", "Feature requires obsolete table support");
        ///     // Test code here
        /// }
        /// </code>
        /// </example>
        protected static void RequireMinimumVersion(string minimumVersion, string? reason = null)
        {
            if (!IsVersionGreaterOrEqual(minimumVersion))
            {
                var message = reason ?? $"Test requires AL version {minimumVersion} or higher.";
                Assert.Ignore(message);
            }
        }

        /// <summary>
        /// Skips the current test if the version exceeds the maximum version
        /// </summary>
        /// <param name="maximumVersion">The maximum supported version (e.g., "16.0.0")</param>
        /// <param name="reason">Optional custom reason for skipping (if null, a default message will be used)</param>
        /// <example>
        /// Use at the beginning of a test method to skip if version is too high:
        /// <code>
        /// [Test]
        /// public void TestLegacyFeature()
        /// {
        ///     RequireMaximumVersion("16.0.0", "Feature was removed in version 16.0.0");
        ///     // Test code here
        /// }
        /// </code>
        /// </example>
        protected static void RequireMaximumVersion(string maximumVersion, string? reason = null)
        {
            if (!IsVersionLessOrEqual(maximumVersion))
            {
                var message = reason ?? $"Test requires AL version {maximumVersion} or lower.";
                Assert.Ignore(message);
            }
        }

        /// <summary>
        /// Skips the current test if the version is outside the specified range
        /// </summary>
        /// <param name="minimumVersion">The minimum required version (inclusive, e.g., "15.0.0")</param>
        /// <param name="maximumVersion">The maximum supported version (inclusive, e.g., "16.5.0")</param>
        /// <param name="reason">Optional custom reason for skipping (if null, a default message will be used)</param>
        /// <example>
        /// Use at the beginning of a test method to skip if version is outside range:
        /// <code>
        /// [Test]
        /// public void TestVersionSpecificFeature()
        /// {
        ///     RequireVersionRange("15.0.0", "16.5.0", "Feature only exists in this version range");
        ///     // Test code here
        /// }
        /// </code>
        /// </example>
        protected static void RequireVersionRange(string minimumVersion, string maximumVersion, string? reason = null)
        {
            var inRange = IsVersionGreaterOrEqual(minimumVersion) && IsVersionLessOrEqual(maximumVersion);
            if (!inRange)
            {
                var message = reason ?? $"Test requires AL version between {minimumVersion} and {maximumVersion}.";
                Assert.Ignore(message);
            }
        }

        /// <summary>
        /// Skips the current test if the Nav.CodeAnalysis version could not be detected
        /// </summary>
        /// <param name="reason">Optional custom reason for skipping (if null, a default message will be used)</param>
        /// <example>
        /// Use at the beginning of a test method that absolutely requires version information:
        /// <code>
        /// [Test]
        /// public void TestVersionDependentFeature()
        /// {
        ///     RequireVersionDetection("This test requires version detection to work properly");
        ///     // Test code here
        /// }
        /// </code>
        /// </example>
        protected static void RequireVersionDetection(string? reason = null)
        {
            if (_navCodeAnalysisVersion == null)
            {
                var message = reason ?? "Test requires Nav.CodeAnalysis version detection to be successful.";
                Assert.Ignore(message);
            }
        }

        #endregion
    }
}