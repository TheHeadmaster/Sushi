using Sushi.Intermediate;

namespace Sushi.Inspection;

/// <summary>
/// Typed formatter metadata for symbols whose details are carried by the generated representation itself.
/// </summary>
public abstract record InspectionSymbolMetadata;

/// <summary>
/// Describes the type and generated display identity of local storage or an SSA temporary.
/// </summary>
/// <param name="Type">
/// The concrete compiler-produced value type.
/// </param>
/// <param name="DisplayName">
/// The generated storage or temporary identity used by the representation.
/// </param>
public sealed record InspectionValueMetadata(IRType Type, string DisplayName) : InspectionSymbolMetadata;

/// <summary>
/// Describes a lowered basic-block label recorded in the control-flow representation.
/// </summary>
/// <param name="Label">
/// The exact label assigned to the block by lowering.
/// </param>
public sealed record InspectionBasicBlockMetadata(string Label) : InspectionSymbolMetadata;
