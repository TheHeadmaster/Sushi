namespace Sushi.Source;

public readonly record struct SourceEncodingIssue(int Start, int Length)
{
    public int End => this.Start + this.Length;
}