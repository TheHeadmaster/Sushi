
using System.Diagnostics;
using FluentAssertions;
using LLVMSharp.Interop;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Backends;
using Sushi.Backends.LLVM;
using Sushi.Backends.Native;
using Sushi.Diagnostics;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;
using Sushi.Parsing;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;
using Sushi.Testing.Parsing;

namespace Sushi.Testing.Compilation;

/// <summary>
/// Verifies value-less return semantics across the source, semantic, IR, LLVM, and native compilation stages.
/// </summary>
[TestFixture]
public class VoidFunctionExecutionTests
{
    private static readonly Uri testUri = new("file:///TestProject/VoidFunction.sus");

    [TestCase("public void Launch() {}", false, TestName = "Parser Should Accept Empty Void Body")]
    [TestCase("public void Launch() { return; }", true, TestName = "Parser Should Accept Bare Return")]
    public void ParserShould_0(string source, bool hasReturn)
    {
        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParsingHelper.ParseFunctionDeclaration(source, testUri);

        diagnostics
            .Should()
            .BeEmpty();

        FunctionDeclarationSyntax function = result.Tree.Root
            .Should()
            .BeOfType<FunctionDeclarationSyntax>()
            .Subject;

        function.ReturnType.Type
            .Should()
            .Be(SyntaxType.VoidKeyword);

        if (hasReturn)
        {
            ReturnStatementSyntax statement = function.Body.Statements
                .Should()
                .ContainSingle()
                .Which
                .Should()
                .BeOfType<ReturnStatementSyntax>()
                .Subject;

            statement.Expression
                .Should()
                .BeNull();

            statement.SemicolonToken.IsMissing
                .Should()
                .BeFalse();
        }
        else
        {
            function.Body.Statements
                .Should()
                .BeEmpty();
        }
    }

    [TestCase("public void Launch() {}", false, TestName = "Void Function Should Bind And Lower Empty Body")]
    [TestCase("public void Launch() { return; }", true, TestName = "Void Function Should Bind And Lower Bare Return")]
    public async Task VoidFunctionShould_0(string declaration, bool hasReturn)
    {
        (SourceAnalysisResult analysis, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(declaration);

        diagnostics
            .Should()
            .BeEmpty();

        BoundFunction function = analysis.Functions
            .Should()
            .ContainSingle()
            .Which;

        function.ReturnType
            .Should()
            .Be(BoundType.Void);

        function.Body.Statements
            .Should()
            .HaveCount(hasReturn ? 1 : 0);

        if (hasReturn)
        {
            BoundReturnStatement statement = function.Body.Statements[0]
                .Should()
                .BeOfType<BoundReturnStatement>()
                .Subject;

            statement.Expression
                .Should()
                .BeNull();
        }

        IRFunction structured = BoundFunctionIRConverter.Convert(function, CancellationToken.None);

        structured.ReturnType
            .Should()
            .Be(IRType.Void);

        structured.Body.Statements
            .Should()
            .HaveCount(hasReturn ? 1 : 0);

        if (hasReturn)
        {
            IRReturnStatement statement = structured.Body.Statements[0]
                .Should()
                .BeOfType<IRReturnStatement>()
                .Subject;

            statement.Expression
                .Should()
                .BeNull();
        }

        LoweredFunction lowered = IRFunctionLowerer.Lower(structured, CancellationToken.None);

        lowered.ReturnType
            .Should()
            .Be(IRType.Void);

        LoweredReturnTerminator terminator = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        terminator.Value
            .Should()
            .BeNull();

        lowered.EntryBlock.Instructions
            .Should()
            .BeEmpty();
    }

    [TestCase("public void Launch() {}", TestName = "LLVM Should Emit Ret Void For Empty Body")]
    [TestCase("public void Launch() { return; }", TestName = "LLVM Should Emit Ret Void For Explicit Return")]
    public async Task LLVMShould_0(string declaration)
    {
        LoweredFunction lowered = await Lower(declaration);

        IModuleEmitter emitter = new LLVMModuleEmitter();

        using IModuleArtifact artifact = emitter.Emit("VoidTestModule", lowered, CancellationToken.None);

        LLVMModuleArtifact module = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        module.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string verificationMessage)
            .Should()
            .BeTrue(verificationMessage);

        string llvm = module.Module.PrintToString();

        llvm
            .Should()
            .Contain("define void @Launch()");

        llvm
            .Should()
            .Contain("ret void");

        llvm
            .Should()
            .NotContain("ret i32 0");
    }

    [TestCase("public void Launch() { return 42; }", ErrorType.ReturnValueInVoidFunction, TestName = "Void Function Should Reject Return Value")]
    [TestCase("public int32 Launch() { return; }", ErrorType.MissingReturnValue, TestName = "Int32 Function Should Reject Bare Return")]
    [TestCase("public bool Launch() { return; }", ErrorType.MissingReturnValue, TestName = "Bool Function Should Reject Bare Return")]
    public async Task BindingShould_0(string declaration, ErrorType expectedError)
    {
        (SourceAnalysisResult analysis, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(declaration);

        analysis.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(expectedError));
    }

    [TestCase("public void Launch() {}", TestName = "Native Executable Should Exit Zero After Empty Void Function")]
    [TestCase("public void Launch() { return; }", TestName = "Native Executable Should Exit Zero After Bare Return")]
    public async Task NativeExecutableShould_0(string declaration)
    {
        LoweredFunction function = await Lower(declaration);
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", Guid.NewGuid().ToString("N"));
        string outputPath = Path.Combine(directory, "VoidFunction");

        try
        {
            NativeExecutableArtifact executable = await new MVPExecutableBuilder().BuildAsync(function, outputPath, CancellationToken.None);

            File.Exists(executable.FilePath)
                .Should()
                .BeTrue();

            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable.FilePath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start()
                .Should()
                .BeTrue();

            await process.WaitForExitAsync();

            process.ExitCode
                .Should()
                .Be(0);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestCase(TestName = "Value Returning Function Should Preserve Original Lowering")]
    public async Task ExistingReturnShould_0()
    {
        LoweredFunction function = await Lower("public int32 Launch() { return 42; }");

        function.ReturnType
            .Should()
            .Be(IRType.Int32);

        LoweredReturnTerminator terminator = function.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        LoweredIntegerConstant result = terminator.Value
            .Should()
            .BeOfType<LoweredIntegerConstant>()
            .Subject;

        result.Value
            .Should()
            .Be(42);
    }

    private static async Task<LoweredFunction> Lower(string declaration)
    {
        (SourceAnalysisResult analysis, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(declaration);

        diagnostics
            .Should()
            .BeEmpty();

        BoundFunction function = analysis.Functions
            .Should()
            .ContainSingle()
            .Which;

        IRFunction structured = BoundFunctionIRConverter.Convert(function, CancellationToken.None);
        return IRFunctionLowerer.Lower(structured, CancellationToken.None);
    }

    private static async Task<(SourceAnalysisResult Analysis, IReadOnlyList<SushiDiagnostic> Diagnostics)> Analyze(string declaration)
    {
        string source = $"""
            package Example;
            namespace Example;

            {declaration}
            """;

        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter reporter = new DiagnosticReporter();
        AnalysisResult result = await new SourceAnalyzer().Analyze(snapshot, reporter, CancellationToken.None);

        return ((SourceAnalysisResult)result, reporter.ReportDiagnostics());
    }
}