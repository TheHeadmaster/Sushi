
using FluentAssertions;
using LLVMSharp.Interop;
using NUnit.Framework;
using Sushi.Backends;
using Sushi.Backends.LLVM;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Testing.Backends.LLVM;

/// <summary>
/// Verifies the native bootstrap calls activity lifecycle methods in their required order.
/// </summary>
[TestFixture]
public class RootActivityEntryShimTests
{
    [TestCase(TestName = "Bootstrap Should Call Entry Then Stop Then Return Zero")]
    public void BootstrapShould_0()
    {
        LoweredFunction entry = CreateVoidFunction("__sushi_root_entry");
        LoweredFunction stop = CreateVoidFunction("__sushi_root_stop");

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit(
            "RootBootstrapTest", [entry, stop], CancellationToken.None);

        RootActivityEntryShim.Add(artifact, entry.Name, stop.Name);

        LLVMModuleArtifact module = (LLVMModuleArtifact)artifact;

        module.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string verificationMessage)
            .Should()
            .BeTrue(verificationMessage);

        string llvm = module.Module.PrintToString();
        int mainIndex = llvm.IndexOf("define i32 @main(", StringComparison.Ordinal);

        mainIndex.Should().BeGreaterThanOrEqualTo(0);

        string nativeMain = llvm[mainIndex..];

        int entryCall = nativeMain.IndexOf("call void @__sushi_root_entry()", StringComparison.Ordinal);
        int stopCall = nativeMain.IndexOf("call void @__sushi_root_stop()", StringComparison.Ordinal);
        int returnInstruction = nativeMain.IndexOf("ret i32 0", StringComparison.Ordinal);

        entryCall.Should().BeGreaterThanOrEqualTo(0);
        stopCall.Should().BeGreaterThan(entryCall);
        returnInstruction.Should().BeGreaterThan(stopCall);
    }

    [TestCase(TestName = "Bootstrap Should Support Root Without Stop Hook")]
    public void BootstrapShould_1()
    {
        LoweredFunction entry = CreateVoidFunction("__sushi_root_entry");

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit(
            "RootWithoutStopTest", entry, CancellationToken.None);

        RootActivityEntryShim.Add(artifact, entry.Name, stopSymbol: null);

        LLVMModuleArtifact module = (LLVMModuleArtifact)artifact;
        string llvm = module.Module.PrintToString();

        llvm.Should().Contain("call void @__sushi_root_entry()");
        llvm.Should().Contain("ret i32 0");

        module.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    private static LoweredFunction CreateVoidFunction(string name)
        => new(
            IRAccessibility.Public,
            name,
            IRType.Void,
            [
                new LoweredBasicBlock(
                    "entry",
                    [],
                    new LoweredReturnTerminator())
            ]);
}