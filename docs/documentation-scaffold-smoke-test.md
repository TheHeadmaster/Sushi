# Documentation Scaffold Manual Smoke Test

1. Open a Sushi source file in VS Code with the extension and language server running. Use a compiler-recognized declaration with no attached documentation.
2. Start a fresh line immediately above the declaration, preserve its indentation, type two slashes, then type the third slash. Confirm the scaffold appears only after that keystroke.
3. For a function with parameters and a non-void return, press Tab and confirm the order is description, each non-discard parameter in declaration order, then return description. Confirm one blank documentation line separates prose from directives.
4. Repeat with void, zero parameters, and an activity declaration. Confirm there is no return directive for void and non-callable declarations receive only the description line.
5. Repeat with LF and CRLF files and an indented declaration. Confirm the existing line ending and indentation remain intact.
6. Put documentation directly above a declaration and type another triple-slash continuation line. Confirm the existing block is not replaced or duplicated.
7. Paste a triple-slash line as one operation above a declaration. Confirm no scaffold is generated.
8. Type a triple-slash line above a detached comment, malformed signature, or unsupported construct. Confirm no scaffold appears.
9. While the language-server request is pending, edit the document or switch editors. Confirm the stale response is discarded. Trigger the same event twice in a test harness and confirm only one insertion occurs.
