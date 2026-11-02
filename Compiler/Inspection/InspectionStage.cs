namespace Sushi.Inspection;

/// <summary>
/// Identifies the compiler representation from which an inspection document
/// was generated. Additional stages will be introduced as their formatters
/// are implemented.
/// </summary>
public enum InspectionStage
{
    StructuredIR,
    LoweredIR
}