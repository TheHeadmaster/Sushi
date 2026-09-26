using Sushi.Configuration.Toml;
using Sushi.Diagnostics;
using Sushi.Source;
using Tomlyn;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace Sushi.Configuration;

public sealed class TomlConfigurationParser
{
    // Placeholder for now, will change the code to pick between SUSE and SUSWARN later
    private const string TomlSyntaxErrorDiagnosticCode = "SUSE1000";

    public TomlConfigurationParseResult Parse(SourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        DocumentSyntax syntax = SyntaxParser.Parse(snapshot.Text, sourceName: snapshot.Uri.LocalPath, validate: true);

        cancellationToken.ThrowIfCancellationRequested();

        TomlConfigurationTable document = BuildDocument(snapshot, syntax, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        SushiDiagnostic[] diagnostics = [.. syntax.Diagnostics.Select(diagnostic => ConvertDiagnostic(snapshot, diagnostic))];

        return new TomlConfigurationParseResult(snapshot, syntax, document, diagnostics);
    }

    private static TomlConfigurationTable BuildDocument(SourceSnapshot snapshot, TomlParser parser, CancellationToken cancellationToken)
    {
        // Placeholder until this gets fleshed out
        return new(
            new Source.SourceSpan()
        );
    }

    private static SushiDiagnostic ConvertDiagnostic(SourceSnapshot snapshot, DiagnosticMessage diagnostic)
    {
        return new SushiDiagnostic(
            TomlSyntaxErrorDiagnosticCode,
            diagnostic.Message,
            diagnostic.Kind switch
            {
                DiagnosticMessageKind.Error => DiagnosticSeverity.Error,
                DiagnosticMessageKind.Warning => DiagnosticSeverity.Warning,
                _ => throw new ArgumentOutOfRangeException(nameof(diagnostic))
            },
            diagnostic.Span.ToSushiSpan(snapshot));
    }

    private readonly record struct KeySegment(string Name, Source.SourceSpan Span);

    private static TomlConfigurationTable BuildDocument(SourceSnapshot snapshot, DocumentSyntax syntax, CancellationToken cancellationToken)
    {
        TomlConfigurationTable root = new(new Source.SourceSpan(snapshot, 0, snapshot.Bytes.Length));

        foreach (KeyValueSyntax keyValue in syntax.KeyValues)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AddKeyValue(snapshot, root, keyValue, cancellationToken);
        }

        foreach (TableSyntaxBase tableSyntax in syntax.Tables)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryGetKeyPath(snapshot, tableSyntax.Name, out List<KeySegment> path))
            {
                continue;
            }

            switch (tableSyntax)
            {
                case TableSyntax table:
                {
                    TomlConfigurationTable? target = GetOrCreateTable(root, path, table.Span.ToSushiSpan(snapshot));

                    if (target is null)
                    {
                        continue;
                    }

                    foreach (KeyValueSyntax keyValue in table.Items)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        AddKeyValue(snapshot, target, keyValue, cancellationToken);
                    }

                    break;
                }

                case TableArraySyntax tableArray:
                {
                    TomlConfigurationTable? target = AddTableArrayElement(root, path, tableArray.Span.ToSushiSpan(snapshot));

                    if (target is null)
                    {
                        continue;
                    }

                    foreach (KeyValueSyntax keyValue in tableArray.Items)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        AddKeyValue(snapshot, target, keyValue, cancellationToken);
                    }

                    break;
                }
            }
        }

        return root;
    }

    private static void AddKeyValue(SourceSnapshot snapshot, TomlConfigurationTable table, KeyValueSyntax keyValue, CancellationToken cancellationToken)
    {
        if (!TryGetKeyPath(snapshot, keyValue.Key, out List<KeySegment> path) || path.Count == 0)
        {
            return;
        }

        TomlConfigurationTable current = table;

        for (int index = 0; index < path.Count - 1; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            KeySegment segment = path[index];

            TomlConfigurationTable? child = GetOrCreateChildTable(current, segment);

            if (child is null)
            {
                return;
            }

            current = child;
        }

        KeySegment leaf = path[^1];

        TomlConfigurationValue value = ConvertValue(snapshot, keyValue.Value, keyValue.Span.ToSushiSpan(snapshot), cancellationToken);

        current.Add(new TomlConfigurationProperty(leaf.Name, leaf.Span, value));
    }

    private static TomlConfigurationValue ConvertValue(SourceSnapshot snapshot, ValueSyntax? value, Source.SourceSpan fallbackSpan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value is null)
        {
            return new TomlConfigurationInvalid(fallbackSpan);
        }

        Source.SourceSpan span = value.Span.ToSushiSpan(snapshot);

        switch (value)
        {
            case StringValueSyntax stringValue:
                return new TomlConfigurationString(stringValue.Value ?? string.Empty, span);

            case ArraySyntax array:
            {
                TomlConfigurationArray result = new(span);

                foreach (ArrayItemSyntax item in array.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    result.Add(ConvertValue(snapshot, item.Value, item.Span.ToSushiSpan(snapshot), cancellationToken));
                }

                return result;
            }

            case InlineTableSyntax inlineTable:
            {
                TomlConfigurationTable result = new(span);

                foreach (InlineTableItemSyntax item in inlineTable.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (item.KeyValue is not null)
                    {
                        AddKeyValue(snapshot, result, item.KeyValue, cancellationToken);
                    }
                }

                return result;
            }

            default:
                return new TomlConfigurationScalar(span);
        }
    }

    private static TomlConfigurationTable? GetOrCreateTable(TomlConfigurationTable root, IReadOnlyList<KeySegment> path, Source.SourceSpan tableSpan)
    {
        TomlConfigurationTable current = root;

        for (int index = 0; index < path.Count; index++)
        {
            KeySegment segment = path[index];

            TomlConfigurationTable? child = GetOrCreateChildTable(current, segment);
            
            if (child is null)
            {
                return null;
            }

            current = child;
        }

        current.Span = tableSpan;

        return current;
    }

    private static TomlConfigurationTable? GetOrCreateChildTable(TomlConfigurationTable table, KeySegment segment)
    {
        if (table.TryGetProperty(segment.Name, out TomlConfigurationProperty existing))
        {
            if (existing.Value is TomlConfigurationTable existingTable)
            {
                return existingTable;
            }

            if (existing.Value is TomlConfigurationArray array && array.Items.LastOrDefault() is TomlConfigurationTable lastTable)
            {
                return lastTable;
            }

            return null;
        }

        TomlConfigurationTable child = new(segment.Span);

        table.Add(new TomlConfigurationProperty(segment.Name, segment.Span, child));

        return child;
    }

    private static TomlConfigurationTable? AddTableArrayElement(TomlConfigurationTable root, IReadOnlyList<KeySegment> path, Source.SourceSpan tableSpan)
    {
        if (path.Count == 0)
        {
            return null;
        }

        TomlConfigurationTable current = root;

        for (int index = 0; index < path.Count - 1; index++)
        {
            TomlConfigurationTable? child = GetOrCreateChildTable(current, path[index]);

            if (child is null)
            {
                return null;
            }

            current = child;
        }

        KeySegment leaf = path[^1];

        TomlConfigurationArray array;

        if (current.TryGetProperty(leaf.Name, out TomlConfigurationProperty existing))
        {
            if (existing.Value is not TomlConfigurationArray existingArray)
            {
                return null;
            }

            array = existingArray;
        }
        else
        {
            array = new TomlConfigurationArray(leaf.Span);

            current.Add(new TomlConfigurationProperty(leaf.Name, leaf.Span, array));
        }

        TomlConfigurationTable element = new(tableSpan);

        array.Add(element);

        return element;
    }

    private static bool TryGetKeyPath(SourceSnapshot snapshot, KeySyntax? key, out List<KeySegment> path)
    {
        path = [];

        if (key?.Key is null)
        {
            return false;
        }

        if (!TryGetKeySegment(snapshot, key.Key, out KeySegment first))
        {
            return false;
        }

        path.Add(first);

        foreach (DottedKeyItemSyntax dotted in key.DotKeys)
        {
            if (!TryGetKeySegment(snapshot, dotted.Key, out KeySegment segment))
            {
                return false;
            }

            path.Add(segment);
        }

        return true;
    }

    private static bool TryGetKeySegment(SourceSnapshot snapshot, BareKeyOrStringValueSyntax? key, out KeySegment segment)
    {
        switch (key)
        {
            case BareKeySyntax bare
                when bare.Key?.Text is { } text:
                segment = new KeySegment(text, bare.Span.ToSushiSpan(snapshot));

                return true;

            case StringValueSyntax quoted
                when quoted.Value is { } value:
                segment = new KeySegment(value, quoted.Span.ToSushiSpan(snapshot));

                return true;

            default:
                segment = default;

                return false;
        }
    }
}