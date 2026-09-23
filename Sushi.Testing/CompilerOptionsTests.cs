using FluentAssertions;
using NUnit.Framework;
using Sushi.Diagnostics.Exceptions;

namespace Sushi.Testing;

[TestFixture]
public class CompilerOptionsTests
{
    [TestCase(TestName = "FromCommandLineArguments Should Throw If Args Is Null")]
    public void FromCommandLineArgumentsShould_0()
    {
        Action action = () => CompilerOptions.FromCommandLineArguments(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [TestCase(TestName = "FromCommandLineArguments Should Throw Stating No Project Parameter If Args Is Empty")]
    public void FromCommandLineArgumentsShould_1()
    {
        Action action = () => CompilerOptions.FromCommandLineArguments([]);

        action
            .Should()
            .Throw<CompilerOptionsException>()
            .WithMessage("A project file or folder path must be specified when compiling. Use the --project \"Path\\To\\File\\OrFolder.susproj\" parameter.")
            .And.Error
            .Should()
            .Be(CompilerOptionsError.InvalidParameterSyntax);
    }
}