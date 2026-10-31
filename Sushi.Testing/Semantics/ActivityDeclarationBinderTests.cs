
using FluentAssertions;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Testing.Semantics;

/// <summary>
/// Verifies activity declaration binding, lifecycle signatures, and executable root uniqueness.
/// </summary>
[TestFixture]
public class ActivityDeclarationBinderTests
{
    [TestCase(TestName = "Activity Binder Should Bind Root Task Activity With Entry And Stop")]
    public async Task ActivityShould_0()
    {
        const string source = """
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public entry void Boot() {}
                public stop void Cleanup() {}
            }
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        diagnostics
            .Should()
            .BeEmpty();

        BoundActivity activity = result.Activities
            .Should()
            .ContainSingle()
            .Which;

        activity.Name
            .Should()
            .Be("Application");

        activity.Accessibility
            .Should()
            .Be(Accessibility.Public);

        activity.IsRoot
            .Should()
            .BeTrue();

        activity.Lifetime
            .Should()
            .Be(ActivityLifetime.Task);

        activity.Entry.Name
            .Should()
            .Be("Boot");

        activity.Entry.IsEntry
            .Should()
            .BeTrue();

        activity.Stop
            .Should()
            .NotBeNull();

        activity.Stop!.Name
            .Should()
            .Be("Cleanup");

        activity.Stop.IsStop
            .Should()
            .BeTrue();

        activity.Span.Snapshot
            .Should()
            .BeSameAs(result.Snapshot);
    }

    [TestCase(TestName = "Activity Binder Should Allow Task Root Without Stop Method")]
    public async Task ActivityShould_1()
    {
        const string source = """
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public entry void Boot() {}
            }
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        diagnostics
            .Should()
            .BeEmpty();

        BoundActivity activity = result.Activities
            .Should()
            .ContainSingle()
            .Which;

        activity.Stop
            .Should()
            .BeNull();
    }

    [TestCase(TestName = "Activity Binder Should Accept Persistent Root As Semantically Valid")]
    public async Task ActivityShould_2()
    {
        const string source = """
            package Example;
            namespace Example;

            public root activity Application : persistent
            {
                public entry void Boot() {}
            }
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        diagnostics
            .Should()
            .BeEmpty();

        BoundActivity activity = result.Activities
            .Should()
            .ContainSingle()
            .Which;

        activity.Lifetime
            .Should()
            .Be(ActivityLifetime.Persistent);

        activity.IsRoot
            .Should()
            .BeTrue();
    }

    [TestCase(TestName = "Activity Binder Should Preserve Non Root Entry Return Type And Parameters")]
    public async Task ActivityShould_3()
    {
        const string source = """
            package Example;
            namespace Example;

            internal activity Worker : persistent
            {
                public entry int32 Calculate(int32 value)
                {
                    return value;
                }
            }
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        diagnostics
            .Should()
            .BeEmpty();

        BoundActivity activity = result.Activities
            .Should()
            .ContainSingle()
            .Which;

        activity.IsRoot
            .Should()
            .BeFalse();

        activity.Accessibility
            .Should()
            .Be(Accessibility.Internal);

        activity.Lifetime
            .Should()
            .Be(ActivityLifetime.Persistent);

        activity.Entry.Declaration.ReturnType.Type
            .Should()
            .Be(SyntaxType.IdentifierToken);

        activity.Entry.Declaration.Parameters
            .Should()
            .ContainSingle();

        activity.Entry.Declaration.Body.Statements
            .Should()
            .ContainSingle();
    }

    [TestCase(TestName = "Activity Binder Should Reject Activity Without Entry")]
    public async Task ActivityShould_4()
    {
        const string source = """
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public stop void Cleanup() {}
            }
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        result.Activities
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.MissingActivityEntry));
    }

    [TestCase(TestName = "Activity Binder Should Reject Multiple Entry Methods")]
    public async Task ActivityShould_5()
    {
        const string source = """
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public entry void First() {}
                public entry void Second() {}
            }
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        result.Activities
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.DuplicateActivityEntry));
    }

    [TestCase(TestName = "Activity Binder Should Diagnose Root Entry Parameters And Non Void Return")]
    public async Task ActivityShould_6()
    {
        const string source = """
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public entry int32 Boot(int32 value)
                {
                    return value;
                }
            }
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        result.Activities
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.InvalidRootEntryParameters));

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.InvalidRootEntryReturnType));
    }

    [TestCase(TestName = "Activity Binder Should Reject Stop Method Returning A Value")]
    public async Task ActivityShould_7()
    {
        const string source = """
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public entry void Boot() {}
                public stop int32 Cleanup()
                {
                    return 42;
                }
            }
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        result.Activities
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.InvalidActivityStopReturnType));
    }

    [TestCase(TestName = "Activity Binder Should Diagnose Multiple Stop Hooks As Unsupported By Current Compiler")]
    public async Task ActivityShould_8()
    {
        const string source = """
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public entry void Boot() {}
                public stop void First() {}
                public stop void Second() {}
            }
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        result.Activities
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.UnsupportedMultipleActivityStopMethods));
    }

    [TestCase(TestName = "Activity Binder Should Not Bind Activity With Missing Lifetime")]
    public async Task ActivityShould_9()
    {
        const string source = """
            package Example;
            namespace Example;

            public root activity Application : 
            {
                public entry void Boot() {}
            }
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        result.Activities
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .Contain(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Activity Binder Should Not Bind Unsupported Instance Field")]
    public async Task ActivityShould_10()
    {
        const string source = """
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public int32 count;
                public entry void Boot() {}
            }
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        result.Activities
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .Contain(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Root Analyzer Should Reject Executable Project Without Root")]
    public async Task ActivityShould_11()
    {
        const string source = """
            package Example;
            namespace Example;

            public activity Worker : task
            {
                public entry void Boot() {}
            }
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> sourceDiagnostics) = await Analyze(source);

        sourceDiagnostics
            .Should()
            .BeEmpty();

        IDiagnosticReporter reporter = new DiagnosticReporter();
        SourceSnapshot project = CreateProjectSnapshot();

        BoundActivity? root = RootActivityAnalyzer.SelectRoot([result], project, reporter, CancellationToken.None);

        root
            .Should()
            .BeNull();

        reporter.ReportDiagnostics()
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.MissingRootActivity));
    }

    [TestCase(TestName = "Root Analyzer Should Reject Multiple Roots Across Files")]
    public async Task ActivityShould_12()
    {
        const string first = """
            package Example;
            namespace Example;

            public root activity First : task
            {
                public entry void Boot() {}
            }
            """;

        const string second = """
            package Example;
            namespace Example;

            public root activity Second : persistent
            {
                public entry void Boot() {}
            }
            """;

        (SourceAnalysisResult firstResult, IReadOnlyList<SushiDiagnostic> firstDiagnostics) = await Analyze(first, "A.sus");
        (SourceAnalysisResult secondResult, IReadOnlyList<SushiDiagnostic> secondDiagnostics) = await Analyze(second, "B.sus");

        firstDiagnostics
            .Should()
            .BeEmpty();

        secondDiagnostics
            .Should()
            .BeEmpty();

        IDiagnosticReporter reporter = new DiagnosticReporter();
        BoundActivity? root = RootActivityAnalyzer.SelectRoot([secondResult, firstResult], CreateProjectSnapshot(), reporter, CancellationToken.None);

        root
            .Should()
            .BeNull();

        SushiDiagnostic duplicate = reporter.ReportDiagnostics()
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.DuplicateRootActivity))
            .Which;

        duplicate.Span.Snapshot
            .Should()
            .BeSameAs(secondResult.Snapshot);
    }

    [TestCase(TestName = "Root Analyzer Should Select Unique Root Independently Of Source Order")]
    public async Task ActivityShould_13()
    {
        const string worker = """
            package Example;
            namespace Example;

            public activity Worker : persistent
            {
                public entry void Boot() {}
            }
            """;

        const string application = """
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public entry void Start() {}
            }
            """;

        (SourceAnalysisResult workerResult, IReadOnlyList<SushiDiagnostic> workerDiagnostics) = await Analyze(worker, "Worker.sus");
        (SourceAnalysisResult applicationResult, IReadOnlyList<SushiDiagnostic> applicationDiagnostics) = await Analyze(application, "Application.sus");

        workerDiagnostics
            .Should()
            .BeEmpty();

        applicationDiagnostics
            .Should()
            .BeEmpty();

        IDiagnosticReporter firstReporter = new DiagnosticReporter();
        IDiagnosticReporter secondReporter = new DiagnosticReporter();
        SourceSnapshot project = CreateProjectSnapshot();

        BoundActivity? first = RootActivityAnalyzer.SelectRoot([workerResult, applicationResult], project, firstReporter, CancellationToken.None);
        BoundActivity? second = RootActivityAnalyzer.SelectRoot([applicationResult, workerResult], project, secondReporter, CancellationToken.None);

        first
            .Should()
            .NotBeNull();

        first!.Name
            .Should()
            .Be("Application");

        second
            .Should()
            .BeSameAs(first);

        firstReporter.ReportDiagnostics()
            .Should()
            .BeEmpty();

        secondReporter.ReportDiagnostics()
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Root Analyzer Should Count Invalid Root Declarations When Checking Uniqueness")]
    public async Task ActivityShould_14()
    {
        const string invalid = """
            package Example;
            namespace Example;

            public root activity Invalid : task
            {
            }
            """;

        const string valid = """
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public entry void Start() {}
            }
            """;

        (SourceAnalysisResult invalidResult, IReadOnlyList<SushiDiagnostic> invalidDiagnostics) = await Analyze(invalid, "A.sus");
        (SourceAnalysisResult validResult, IReadOnlyList<SushiDiagnostic> validDiagnostics) = await Analyze(valid, "B.sus");

        invalidDiagnostics
            .Should()
            .Contain(diagnostic => diagnostic.IsType(ErrorType.MissingActivityEntry));

        validDiagnostics
            .Should()
            .BeEmpty();

        IDiagnosticReporter reporter = new DiagnosticReporter();
        BoundActivity? root = RootActivityAnalyzer.SelectRoot([invalidResult, validResult], CreateProjectSnapshot(), reporter, CancellationToken.None);

        root
            .Should()
            .BeNull();

        reporter.ReportDiagnostics()
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.DuplicateRootActivity));
    }

    [TestCase(TestName = "Activity Binder Should Decode Escaped Activity And Lifecycle Method Names")]
    public async Task ActivityShould_15()
    {
        const string source = """
            package Example;
            namespace Example;

            public root activity @task : task
            {
                public entry void @stop() {}
            }
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        diagnostics
            .Should()
            .BeEmpty();

        BoundActivity activity = result.Activities
            .Should()
            .ContainSingle()
            .Which;

        activity.Name
            .Should()
            .Be("task");

        activity.Entry.Name
            .Should()
            .Be("stop");
    }

    [TestCase(TestName = "Activity Binder Should Locate Missing File Headers At Activity Declaration")]
    public async Task ActivityShould_16()
    {
        const string source = """
            public root activity Application : task
            {
                public entry void Boot() {}
            }
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        result.Activities
            .Should()
            .ContainSingle();

        SushiDiagnostic missingPackage = diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.MissingPackageDeclaration))
            .Which;

        SushiDiagnostic missingNamespace = diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.MissingNamespaceDeclaration))
            .Which;

        missingPackage.Span.Start
            .Should()
            .Be(0);

        missingNamespace.Span.Start
            .Should()
            .Be(0);
    }

    private static async Task<(SourceAnalysisResult Result, IReadOnlyList<SushiDiagnostic> Diagnostics)> Analyze(string source, string filename = "Application.sus")
    {
        Uri uri = new($"file:///TestProject/{filename}");
        SourceSnapshot snapshot = SourceSnapshot.FromText(uri, version: null, source);
        IDiagnosticReporter reporter = new DiagnosticReporter();

        AnalysisResult analysis = await new SourceAnalyzer().Analyze(snapshot, reporter, CancellationToken.None);

        return ((SourceAnalysisResult)analysis, reporter.ReportDiagnostics());
    }

    private static SourceSnapshot CreateProjectSnapshot()
        => SourceSnapshot.FromText(new Uri("file:///TestProject/.susproj"), version: null, "name = \"Example\"");
}
