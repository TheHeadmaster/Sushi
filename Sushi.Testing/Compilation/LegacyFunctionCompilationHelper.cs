
using Sushi.Analysis;
using Sushi.Backends.Native;
using Sushi.Compilation;
using Sushi.Diagnostics;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Testing.Compilation;

/// <summary>
/// Compiles a standalone Sushi function through the executable backend without
/// imposing the canonical project-level root activity requirement.
/// </summary>
internal static class LegacyFunctionCompilationHelper
{
    private static readonly Uri sourceUri = new("file:///TestProject/LegacyFunction.sus");

    /// <summary>
    /// Analyzes a source file containing one function and produces a native executable
    /// using the temporary function-entry convention retained for compiler tests.
    /// </summary>
    /// <param name="source">
    /// The complete Sushi source, including package and namespace declarations.
    /// </param>
    /// <param name="outputPath">
    /// The destination for the generated executable.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels analysis, lowering, and native compilation.
    /// </param>
    /// <returns>
    /// The diagnostic and executable results of the test compilation.
    /// </returns>
    public static async Task<MVPCompilationResult> CompileAsync(string source, string outputPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);

        SourceSnapshot snapshot = SourceSnapshot.FromText(sourceUri, version: null, source);
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();

        SourceAnalysisResult analysis = (SourceAnalysisResult)await new SourceAnalyzer().Analyze(
            snapshot, diagnosticReporter, cancellationToken);

        if (diagnosticReporter.HasErrors())
        {
            return new MVPCompilationResult(diagnosticReporter.ReportDiagnostics(), executable: null);
        }

        BoundFunction function = analysis.Functions.Single();
        IRFunction structured = BoundFunctionIRConverter.Convert(function, cancellationToken);
        LoweredFunction lowered = IRFunctionLowerer.Lower(structured, cancellationToken);

        NativeExecutableArtifact executable = await new MVPExecutableBuilder().BuildAsync(
            lowered, outputPath, cancellationToken);

        return new MVPCompilationResult(diagnosticReporter.ReportDiagnostics(), executable);
    }
}
