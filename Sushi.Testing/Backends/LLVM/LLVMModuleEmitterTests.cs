using FluentAssertions;
using LLVMSharp.Interop;
using NUnit.Framework;
using Sushi.Backends.LLVM;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Testing.Backends.LLVM;

[TestFixture]
public class LLVMModuleEmitterTests
{
    [TestCase(TestName = "LLVM Module Emitter Should Emit Minimum Lowered Function")]
    public void EmitShould_0()
    {
        LoweredFunction function = new(
            IRAccessibility.Public,
            "main",
            IRType.Int32,
            [
                new LoweredBasicBlock(
                    "entry",
                    [],
                    new LoweredReturnTerminator(new LoweredIntegerConstant(42)))
            ]);

        using LLVMModuleArtifact artifact = new LLVMModuleEmitter().Emit("TestModule", function, CancellationToken.None);

        artifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string verificationMessage)
            .Should()
            .BeTrue(verificationMessage);

        string llvm = artifact.Module.PrintToString();

        llvm
            .Should()
            .Contain("define i32 @main()");

        llvm
            .Should()
            .Contain("entry:");

        llvm
            .Should()
            .Contain("ret i32 42");
    }
}