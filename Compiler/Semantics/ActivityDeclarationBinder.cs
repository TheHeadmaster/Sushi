
using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;

namespace Sushi.Semantics;

/// <summary>
/// Binds activity identities, lifetime policies, and lifecycle method signatures.
/// Executable method bodies are handled by a later compiler stage.
/// </summary>
public static class ActivityDeclarationBinder
{
    /// <summary>
    /// Validates a parsed activity and returns its semantic representation when complete.
    /// </summary>
    /// <param name="declaration">
    /// The activity declaration to validate.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives activity-specific semantic diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels semantic binding.
    /// </param>
    /// <returns>
    /// The bound activity, or null when its declaration cannot be represented correctly.
    /// </returns>
    public static BoundActivity? Bind(ActivityDeclarationSyntax declaration, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        if (!HasCompleteHeader(declaration)
            || !TryBindAccessibility(declaration.AccessModifier, out Accessibility accessibility)
            || !TryBindLifetime(declaration.LifetimeKeyword, out ActivityLifetime lifetime))
        {
            return null;
        }

        string? name = IdentifierBinder.Bind(declaration.Name);

        if (name is null)
        {
            return null;
        }

        List<ActivityMethodDeclarationSyntax> entries = [];
        List<ActivityMethodDeclarationSyntax> stops = [];
        bool valid = true;

        foreach (ActivityMemberSyntax member in declaration.Members)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (member)
            {
                case ActivityMethodDeclarationSyntax method when method.LifecycleModifier.Type is SyntaxType.EntryKeyword:
                    entries.Add(method);
                    break;

                case ActivityMethodDeclarationSyntax method when method.LifecycleModifier.Type is SyntaxType.StopKeyword:
                    stops.Add(method);
                    break;

                default:
                    valid = false;
                    break;
            }
        }

        if (entries.Count == 0)
        {
            diagnosticReporter.GenerateError(ErrorType.MissingActivityEntry, declaration.ActivityKeyword.Span);
            valid = false;
        }

        for (int i = 1; i < entries.Count; i++)
        {
            diagnosticReporter.GenerateError(ErrorType.DuplicateActivityEntry, entries[i].LifecycleModifier.Span);
            valid = false;
        }

        for (int i = 1; i < stops.Count; i++)
        {
            diagnosticReporter.GenerateError(ErrorType.UnsupportedMultipleActivityStopMethods, stops[i].LifecycleModifier.Span);
            valid = false;
        }

        if (declaration.RootKeyword is { IsSourceBacked: true })
        {
            foreach (ActivityMethodDeclarationSyntax entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.Parameters.Count != 0)
                {
                    diagnosticReporter.GenerateError(ErrorType.InvalidRootEntryParameters, entry.OpenParenthesisToken.Span);
                    valid = false;
                }

                if (entry.ReturnType.Type is not SyntaxType.VoidKeyword || !entry.ReturnType.IsSourceBacked)
                {
                    diagnosticReporter.GenerateError(ErrorType.InvalidRootEntryReturnType, entry.ReturnType.Span);
                    valid = false;
                }
            }
        }

        foreach (ActivityMethodDeclarationSyntax stop in stops)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (stop.ReturnType.Type is not SyntaxType.VoidKeyword || !stop.ReturnType.IsSourceBacked)
            {
                diagnosticReporter.GenerateError(ErrorType.InvalidActivityStopReturnType, stop.ReturnType.Span);
                valid = false;
            }
        }

        if (!valid)
        {
            return null;
        }

        BoundActivityMethod? entryMethod = BindMethod(entries[0]);
        BoundActivityMethod? stopMethod = stops.Count == 0 ? null : BindMethod(stops[0]);

        if (entryMethod is null || (stops.Count != 0 && stopMethod is null))
        {
            return null;
        }

        return new BoundActivity(name, accessibility, lifetime, entryMethod, stopMethod, declaration);
    }

    private static BoundActivityMethod? BindMethod(ActivityMethodDeclarationSyntax declaration)
    {
        if (!declaration.AccessModifier.IsSourceBacked
            || !declaration.LifecycleModifier.IsSourceBacked
            || !declaration.ReturnType.IsSourceBacked
            || !declaration.OpenParenthesisToken.IsSourceBacked
            || !declaration.CloseParenthesisToken.IsSourceBacked
            || !declaration.Body.OpenBraceToken.IsSourceBacked
            || !declaration.Body.CloseBraceToken.IsSourceBacked
            || declaration.Parameters.Any(parameter => !parameter.TypeToken.IsSourceBacked || !parameter.NameToken.IsSourceBacked)
            || declaration.ParameterSeparators.Any(separator => !separator.IsSourceBacked)
            || !TryBindAccessibility(declaration.AccessModifier, out Accessibility accessibility))
        {
            return null;
        }

        string? name = IdentifierBinder.Bind(declaration.Name);

        return name is null ? null : new BoundActivityMethod(name, accessibility, declaration);
    }

    private static bool HasCompleteHeader(ActivityDeclarationSyntax declaration)
        => declaration.AccessModifier.IsSourceBacked
            && (declaration.RootKeyword is null || declaration.RootKeyword.Value.IsSourceBacked)
            && declaration.ActivityKeyword.IsSourceBacked
            && declaration.Name.IsSourceBacked
            && declaration.ColonToken.IsSourceBacked
            && declaration.LifetimeKeyword.IsSourceBacked
            && declaration.OpenBraceToken.IsSourceBacked
            && declaration.CloseBraceToken.IsSourceBacked;

    private static bool TryBindLifetime(SyntaxToken token, out ActivityLifetime lifetime)
    {
        if (token.IsSourceBacked)
        {
            switch (token.Type)
            {
                case SyntaxType.TaskKeyword:
                    lifetime = ActivityLifetime.Task;
                    return true;

                case SyntaxType.PersistentKeyword:
                    lifetime = ActivityLifetime.Persistent;
                    return true;
            }
        }

        lifetime = default;
        return false;
    }

    private static bool TryBindAccessibility(SyntaxToken token, out Accessibility accessibility)
    {
        if (!token.IsSourceBacked)
        {
            accessibility = default;
            return false;
        }

        ReadOnlySpan<byte> spelling = token.Span.Snapshot.Bytes.Span[token.Span.Start..token.Span.End];

        if (spelling.SequenceEqual("public"u8))
        {
            accessibility = Accessibility.Public;
            return true;
        }

        if (spelling.SequenceEqual("internal"u8))
        {
            accessibility = Accessibility.Internal;
            return true;
        }

        if (spelling.SequenceEqual("package"u8))
        {
            accessibility = Accessibility.Package;
            return true;
        }

        if (spelling.SequenceEqual("protected"u8))
        {
            accessibility = Accessibility.Protected;
            return true;
        }

        if (spelling.SequenceEqual("private"u8))
        {
            accessibility = Accessibility.Private;
            return true;
        }

        accessibility = default;
        return false;
    }
}
