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
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Testing.Intermediate;

[TestFixture]
public sealed class RuntimeBooleanParameterTests
{
    private static readonly Uri testUri = new("file:///TestProject/RuntimeBooleanParameters.sus");

    [TestCase("public bool echo(bool value) { return value; }", 0, TestName = "Compiler Should Preserve First Parameter Through Lowering")]
    [TestCase("public bool choose(bool first, bool second) { return second; }", 1, TestName = "Compiler Should Preserve Second Parameter Through Lowering")]
    public async Task ConvertShould_0(string declaration, int expectedIndex)
    {
        BoundFunction bound = await Bind(declaration);
        IRFunction structured = BoundFunctionIRConverter.Convert(bound, CancellationToken.None);
        LoweredFunction lowered = IRFunctionLowerer.Lower(structured, CancellationToken.None);

        IRReturnStatement irReturn = structured.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<IRReturnStatement>()
            .Subject;

        IRParameterReferenceExpression irReference = irReturn.Expression
            .Should()
            .BeOfType<IRParameterReferenceExpression>()
            .Subject;

        irReference.Index
            .Should()
            .Be(expectedIndex);

        irReference.Type
            .Should()
            .Be(IRType.Bool);

        LoweredReturnTerminator terminator = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        LoweredParameterValue runtimeValue = terminator.Value
            .Should()
            .BeOfType<LoweredParameterValue>()
            .Subject;

        runtimeValue.Index
            .Should()
            .Be(expectedIndex);

        runtimeValue.Type
            .Should()
            .Be(IRType.Bool);

        lowered.EntryBlock.Instructions
            .Should()
            .BeEmpty();

        lowered.Parameters
            .Should()
            .HaveCount(bound.Parameters.Count);

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("RuntimeBooleanParameters", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmArtifact = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        llvmArtifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string verificationMessage)
            .Should()
            .BeTrue(verificationMessage);

        llvmArtifact.Module.PrintToString()
            .Should()
            .Contain("ret i1 %");
    }

    [TestCase("public bool echo(bool value) { return value; }", false, false, 0, TestName = "Native Caller Should Return False Argument")]
    [TestCase("public bool echo(bool value) { return value; }", true, false, 1, TestName = "Native Caller Should Return True Argument")]
    [TestCase("public bool choose(bool first, bool second) { return second; }", true, false, 0, TestName = "Native Caller Should Return False Second Argument")]
    [TestCase("public bool choose(bool first, bool second) { return second; }", false, true, 1, TestName = "Native Caller Should Return True Second Argument")]
    public async Task NativeShould_0(string declaration, bool firstArgument, bool secondArgument, int expectedExitCode)
    {
        BoundFunction bound = await Bind(declaration);
        LoweredFunction lowered = IRFunctionLowerer.Lower(BoundFunctionIRConverter.Convert(bound, CancellationToken.None), CancellationToken.None);

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("RuntimeParameterNative", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmArtifact = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        AddTestOnlyCaller(llvmArtifact, lowered, firstArgument, secondArgument);

        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", Guid.NewGuid().ToString("N"));
        string objectPath = Path.Combine(directory, "runtime-parameter.obj");
        string executablePath = Path.Combine(directory, "RuntimeParameter");

        try
        {
            NativeObjectArtifact objectFile = new LLVMObjectEmitter().EmitForHost(artifact, objectPath, CancellationToken.None);
            NativeExecutableArtifact executable = await ClangNativeLinker.LinkAsync(objectFile, executablePath, CancellationToken.None);

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
                .Be(expectedExitCode);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestCase("public bool echo(bool value) { return value and true; }", TestName = "Runtime Logical And Should Generate Conditional Control Flow")]
    [TestCase("public bool echo(bool value) { return value or false; }", TestName = "Runtime Logical Or Should Generate Conditional Control Flow")]
    public async Task LowerShould_0(string declaration)
    {
        BoundFunction bound = await Bind(declaration);
        IRFunction structured = BoundFunctionIRConverter.Convert(bound, CancellationToken.None);
        LoweredFunction lowered = IRFunctionLowerer.Lower(structured, CancellationToken.None);

        lowered.Blocks
            .Should()
            .HaveCount(4);

        lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredConditionalBranchTerminator>();

        lowered.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<LoweredBooleanPhiInstruction>()
            .Should()
            .ContainSingle();

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("LogicalParameter", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmArtifact = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        string llvm = llvmArtifact.Module.PrintToString();

        llvm
            .Should()
            .Contain("br i1");

        llvm
            .Should()
            .Contain("phi i1");
    }

    [TestCase(TestName = "Native MVP Builder Should Reject Parameterized Function Entry")]
    public async Task NativeShould_1()
    {
        BoundFunction bound = await Bind("public bool echo(bool value) { return value; }");
        LoweredFunction lowered = IRFunctionLowerer.Lower(BoundFunctionIRConverter.Convert(bound, CancellationToken.None), CancellationToken.None);

        Func<Task> action = async () => await new MVPExecutableBuilder().BuildAsync(lowered, "unused", CancellationToken.None);

        await action
            .Should()
            .ThrowAsync<NotSupportedException>();
    }

    private static void AddTestOnlyCaller(LLVMModuleArtifact artifact, LoweredFunction function, bool firstArgument, bool secondArgument)
    {
        LLVMModuleRef module = artifact.Module;
        LLVMContextRef context = module.Context;
        LLVMValueRef callee = module.GetNamedFunction(function.Name);

        LLVMTypeRef[] parameters = [.. function.Parameters.Select(parameter => parameter.Type is IRType.Bool ? context.Int1Type : throw new NotSupportedException())];
        LLVMTypeRef calleeType = LLVMTypeRef.CreateFunction(context.Int1Type, parameters);
        LLVMTypeRef mainType = LLVMTypeRef.CreateFunction(context.Int32Type, [context.Int32Type, context.CreatePointerType(0)]);
        LLVMValueRef main = module.AddFunction("main", mainType);
        LLVMBasicBlockRef entry = main.AppendBasicBlock("entry");

        using LLVMBuilderRef builder = context.CreateBuilder();

        builder.PositionAtEnd(entry);

        LLVMValueRef[] arguments = function.Parameters.Count switch
        {
            1 => [LLVMValueRef.CreateConstInt(context.Int1Type, firstArgument ? 1UL : 0UL)],
            2 => [LLVMValueRef.CreateConstInt(context.Int1Type, firstArgument ? 1UL : 0UL), LLVMValueRef.CreateConstInt(context.Int1Type, secondArgument ? 1UL : 0UL)],
            _ => throw new NotSupportedException("The integration test supports one or two Boolean arguments.")
        };

        LLVMValueRef result = builder.BuildCall2(calleeType, callee, arguments, "sushi.test.result");

        builder.BuildRet(builder.BuildZExt(result, context.Int32Type, "sushi.test.exitcode"));

        module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    private static async Task<BoundFunction> Bind(string declaration)
    {
        string source = $"package Example; namespace Example; {declaration}";
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter reporter = new DiagnosticReporter();

        SourceAnalysisResult result = (SourceAnalysisResult)await new SourceAnalyzer().Analyze(snapshot, reporter, CancellationToken.None);

        reporter.ReportDiagnostics()
            .Should()
            .BeEmpty();

        return result.Functions
            .Should()
            .ContainSingle()
            .Which;
    }
}