using FluentAssertions;
using NUnit.Framework;
using Sushi.Backends.LLVM;
using Sushi.Backends.Native;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Testing.Backends.LLVM;

[TestFixture]
public class LLVMObjectEmitterTests
{
    [TestCase(TestName = "LLVM Object Emitter Should Emit Host Native Object File")]
    public void EmitForHostShould_0()
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

        using LLVMModuleArtifact moduleArtifact = new LLVMModuleEmitter().Emit("TestModule", function, CancellationToken.None);

        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", Guid.NewGuid().ToString("N"));
        string objectPath = Path.Combine(directory, OperatingSystem.IsWindows() ? "TestModule.obj" : "TestModule.o");

        try
        {
            NativeObjectArtifact objectArtifact = LLVMObjectEmitter.EmitForHost(moduleArtifact, objectPath, CancellationToken.None);

            objectArtifact.FilePath
                .Should()
                .Be(Path.GetFullPath(objectPath));

            objectArtifact.TargetTriple
                .Should()
                .NotBeNullOrWhiteSpace();

            moduleArtifact.Module.Target
                .Should()
                .Be(objectArtifact.TargetTriple);

            moduleArtifact.Module.DataLayout
                .Should()
                .NotBeNullOrWhiteSpace();

            File.Exists(objectArtifact.FilePath)
                .Should()
                .BeTrue();

            new FileInfo(objectArtifact.FilePath).Length
                .Should()
                .BeGreaterThan(0);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}