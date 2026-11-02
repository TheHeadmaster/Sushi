# Project-aware semantic diagnostics smoke test

This is a manual VS Code procedure for a configured Sushi project. It does not claim that graphical validation has been performed.

## Cross-file function calls

Create a `.susproj` that includes both source files and use these files in the same package and namespace:

`InspectMe.sus`

```sushi
package Example;
namespace Example;

public int32 InspectMe(int32 input)
{
    int32 doubled = InspectMe2(input);

    if (doubled > 10)
    {
        return doubled;
    }

    return doubled + 1;
}
```

`InspectMe2.sus`

```sushi
package Example;
namespace Example;

public int32 InspectMe2(int32 input)
{
    return input * 2;
}
```

1. Open both files in VS Code. Verify `InspectMe.sus` has no `UnsupportedMVPExpression` diagnostic for the call and no cascading unresolved-local diagnostics for `doubled`.
2. Move both functions into one source file and verify the same result.
3. Change `InspectMe2`'s parameter type from `int32` to `bool` without saving. Verify the caller reports an argument-type diagnostic.
4. Restore the parameter type to `int32` and verify the caller diagnostic clears.
5. Add a call to an unknown function or use a value-returning call as a standalone statement. Verify the compiler still reports a useful error.
6. Confirm `Application` remains classified as a `class` semantic token and that function, parameter, local, and namespace highlighting still work.

## Inspection regression check

7. Open Structured IR and Lowered IR for the caller. Confirm both documents remain read-only and syntax-highlighted.
8. Exercise same-stage navigation to the callee from each representation, then confirm the lazily opened target uses the matching IR language.
9. Confirm editing source diagnostics does not refresh already-open inspection documents.
10. Confirm diagnostic analysis creates no `bin` output, executable, or native object file.

Record the project and VS Code versions and mark each step passed or failed. Automated tests do not substitute for this graphical check.
