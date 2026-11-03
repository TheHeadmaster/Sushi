# Compiler Inspection: VS Code smoke test

This is a manual procedure, not a claim of completed GUI validation. The automated tests exercise the compiler/LSP boundary and mock only VS Code's host API for client command tests.

1. From the repository root, run `npm ci`, `npm run generate:syntax`, and `npm run compile`. Build the compiler with `dotnet build Sushi.slnx --configuration Debug`.
2. Start the existing **Sushi: Extension and LSP** debug configuration. It starts the TCP language server on `127.0.0.1:5057` and then launches the Extension Development Host using the existing VS Code tasks. Alternatively, start the server with `dotnet run --project Compiler/Sushi.csproj --configuration Debug -- -lsp --tcp 127.0.0.1:5057`, then launch **Sushi: VS Code Extension**. The existing launch tasks are Windows-oriented; on other platforms, launch `code --new-window TestProject --extensionDevelopmentPath=<absolute-repository-path>` after starting the server. Development mode uses port 5057, or `SUSHI_LSP_DEBUG_PORT` when set.
3. In the Extension Development Host, open the folder containing a valid `.susproj`. Confirm its source include/exclude configuration selects the `.sus` file being tested. A root activity is not required for inspection.
4. Open or create a source file within that configured source set; save it once if it is newly created so project discovery can select its file path. For a simple example, use `package Example; namespace Example; public int32 InspectMe(int32 input) { int32 value = input + 1; return value; }`. Put the cursor inside the function body. Record the inspected project's native output directory/file set for comparison.
5. Right-click and invoke **Sushi: Show Structured IR**. Verify the source stays open with its selection intact and a new `sushi-inspect:` document opens beside it. Also invoke the same command from the Command Palette while the source editor is active.
6. Verify the generated document is read-only, its language mode is **Sushi Structured IR**, and function/type/local/operator tokens receive highlighting. Try typing into it; no edit should be applied. Use **Developer: Inspect Editor Tokens and Scopes** to check grammar scopes if the current theme renders similar colors.
7. Return focus to the source function and invoke **Sushi: Show Lowered IR**. Verify a different virtual document opens, with **Sushi Lowered IR** mode, block labels, SSA temporaries, explicit instructions, and appropriate highlighting. For a function with shifts, check the distinct `checked_shift <<` and `count_checked_shift >>` labels.
8. Change `input + 1` to `input + 2` without saving. Invoke either command again. Verify the new document reflects `2` while the old document still contains `1`. When testing a call, unsaved edits to the dependency's supported signature should also be honored.
9. Put the cursor on whitespace outside all functions or within an activity lifecycle method. Request inspection and verify a useful explanation appears and no empty virtual document opens. Source errors and version mismatches should likewise fail clearly; retry after synchronization if requested.
10. Verify inspection actions are absent from generated documents, `.susproj`/`.susln` editors, and unrelated languages. With a source included by multiple projects, verify the project picker requires an explicit choice. Try canceling an in-progress request.
11. Close generated tabs and request inspection again. Closing releases their server-side metadata; changing language during initial opening must not release it. The session retains at most 32 artifacts, rejecting additional requests with a close-document explanation instead of replacing older artifacts. Shutdown releases the entire session.
12. Compare the inspected project's files with the recorded baseline. No executable, native object, or link output should have been created by inspection. Compiler/server build artifacts from step 1 are separate and expected.

The initial documents are frozen snapshots. Automatic refresh and cross-stage navigation remain outside the current inspection feature.
# Same-stage semantic navigation

Use a real Sushi project containing package functions with parameters, local variables, control flow, and a call to a function in another source file.

1. Open Structured IR for a function with parameters and local variables.
2. Ctrl+click a parameter reference and verify it selects the parameter definition.
3. Ctrl+click a local reference and verify it selects the matching local declaration.
4. Open Lowered IR for a function containing control flow.
5. Ctrl+click an SSA temporary use and verify it selects the defining instruction.
6. Ctrl+click a branch target and verify it selects the corresponding basic-block label.
7. Inspect a caller that references a function in a different source file.
8. Ctrl+click the function reference and verify the callee opens as Structured IR.
9. Repeat the cross-file test from Lowered IR and verify the callee opens as Lowered IR.
10. Modify the callee source without saving, then navigate from an older caller inspection. Verify the target still reflects the caller artifact's captured snapshots.
11. Verify F12 and Peek Definition use the same resolution behavior as Ctrl+click.
12. Click whitespace or punctuation and verify there is no misleading navigation.
13. Close generated documents, then confirm new inspections and navigation still work.
14. Confirm navigation does not generate a native executable or object file.

Record the Sushi project and VS Code version used, and mark each step as passed or failed. This procedure requires a graphical VS Code session; automated protocol and provider tests do not replace this manual check.

# Snapshot-aware cache reuse

Use a real Sushi project with an `InspectMe()` function and, for the snapshot check, a called function in another source file. This is a manual procedure; automated cache tests do not replace graphical validation.

1. Open Structured IR for `InspectMe()` and verify its text and highlighting.
2. Open Lowered IR for the same function. Verify it is a separate, correct, read-only document with Lowered IR highlighting.
3. Request Structured IR and Lowered IR again. Verify both remain correct and each request opens an independent document.
4. Change `InspectMe()` without saving and request an inspection. Verify the new representation reflects the edit and all earlier documents remain unchanged.
5. Change the called function's signature without saving and request a new inspection. Verify the new inspection uses the current signature.
6. Navigate from an older caller representation to the called function. Verify the generated target still reflects the caller artifact's original captured source snapshot.
7. Close generated documents and inspect the function again. Verify inspection still works.
8. Confirm inspection and navigation did not generate a native executable or object file.

Record the Sushi project and VS Code version used, and mark each step as passed or failed. Do not mark this procedure passed unless it was exercised in a graphical VS Code session.

# Generated IR to Source Navigation

Use a configured Sushi project with package functions in at least two source files. This procedure requires a graphical VS Code Extension Development Host; protocol and client tests do not replace it.

1. Inspect a caller as Structured IR, place the cursor on the generated callee function symbol, and invoke **Sushi: Go to Source Definition**. Verify the exact source name token is selected.
2. Repeat from Lowered IR and verify the command opens the same source declaration.
3. Invoke the command on the caller's generated function declaration and verify it selects the caller's source name.
4. Place the cursor on a generated call argument label such as `arg0`. Verify it selects the matching parameter in the callee, including when the callee is in another file.
5. Verify Ctrl+click and F12 still navigate to same-stage generated IR, not to Sushi source.
6. Change the callee source without saving, then navigate from the older IR document. Verify a distinct read-only `sushi-source-snapshot:` document opens with the original text and declaration range.
7. Inspect the changed caller again and navigate to the callee. Verify this newer navigation uses the newer source snapshot.
8. Restore the original source content and verify navigation can use the editable source document when its actual text matches the inspection snapshot.
9. Invoke the source command on a local, SSA temporary, basic block, whitespace, or punctuation. Verify no invented source location appears.
10. Close a historical preview and verify its retained client content is released. Closing the originating IR document must not close an already-open historical preview.
11. Confirm earlier generated IR documents remain read-only and unchanged, and that hover behavior still works.
12. Confirm source navigation generated no native executable or object output.
13. Open both `Caller.sus` and `Helper.sus`, open Caller Structured IR, and navigate to Helper IR and back several times. Verify existing same-stage IR tabs are focused and identical tabs do not accumulate.
14. Invoke Go to Source Definition for Helper while its source tab is already open. Verify that tab receives focus without a second source mirror; repeat with the source tab inactive and with the source file closed.
15. Modify Helper without saving and navigate from the older IR. Verify a separate historical preview opens; repeat navigation and verify the same preview is focused. Inspect a different source snapshot and confirm it remains separate.

Record the Sushi project and VS Code version used, and mark each step as passed or failed. Do not mark this procedure passed unless exercised in an actual VS Code Extension Development Host.

# Generated IR semantic hovers

Use a configured Sushi project with a caller and callee, local storage, and a function that lowers to multiple basic blocks and SSA temporaries. Hover information comes from captured compiler identities and formatter metadata; this procedure requires a graphical VS Code session.

1. Open Structured IR for a function with parameters and a local variable. Hover over the function name, parameter declaration and reference, and local declaration and reference. Verify the function hover shows its source-level accessibility, return type, and ordered typed parameters, while parameter and local hovers show their type and owner.
2. Hover over a call's generated function symbol and an `argN` call-argument label. Verify the function signature and the specifically indexed callee parameter are shown, even when the callee is in another source file.
3. Open Lowered IR for a function with control flow. Hover over local-slot references, SSA temporary definitions and uses, and block labels and branch targets. Verify type and owner details, and verify each block hover shows its label and index.
4. Hover over punctuation, whitespace, and positions outside the generated text. Verify no hover appears.
5. Close a generated document and verify hovering another still-open inspection continues to work. Reopen an inspection and verify its hover metadata is available.
6. Verify hovers do not generate a referenced function, change the displayed text, or create native executable or object output.

Record the Sushi project and VS Code version used, and mark each step as passed or failed. Protocol tests exercise the actual language-server request; they do not replace graphical validation.

# Dependency-aware cache reuse

Use a configured project containing `Calculate()` calling `Double()` and an unrelated function. This manual procedure checks visible results and historical navigation; automated tests establish whether compiler artifacts were actually reused.

1. Inspect `Calculate()` as Structured IR, then as Lowered IR.
2. Change only the unrelated function body without saving, inspect `Calculate()` again, and verify the representation remains correct.
3. Change `Double()`'s implementation without changing its signature. Inspect `Calculate()` again and verify it remains correct.
4. Navigate from the new inspection to `Double()` and verify the target contains the current implementation. Navigate from the original inspection and verify its target still contains the original implementation.
5. Change `Double()`'s signature and verify a new `Calculate()` inspection does not reuse an incompatible binding.
6. Restore the signature and verify inspection succeeds again.
7. Confirm earlier generated documents remain read-only and unchanged, then close them and inspect again.
8. Confirm no executable or native object file was generated.

Record the Sushi project and VS Code version used, and mark each step as passed or failed. Do not mark this procedure passed unless it was exercised in a graphical VS Code session.
