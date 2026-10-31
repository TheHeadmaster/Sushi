using FluentAssertions;

[assembly: FluentAssertions.Extensibility.AssertionEngineInitializer(
    typeof(AssertionEngineInitializer),
    nameof(AssertionEngineInitializer.AcknowledgeSoftWarning))]

#pragma warning disable CA1050 // Declare types in namespaces
public static class AssertionEngineInitializer
#pragma warning restore CA1050 // Declare types in namespaces
{
    public static void AcknowledgeSoftWarning() => License.Accepted = true;
}