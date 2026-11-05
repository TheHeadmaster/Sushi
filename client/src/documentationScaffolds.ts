import * as vscode from 'vscode';
import { LanguageClient } from 'vscode-languageclient/node';
import {
	createDocumentationScaffoldSnippet,
	DocumentationScaffoldRequestGate,
	identifyThirdSlashTyping,
	isScaffoldResponseCurrent,
	DocumentationScaffoldResponse
} from './documentationScaffoldLogic';

export function registerDocumentationScaffolds(context: vscode.ExtensionContext, client: LanguageClient): void {
	const requestGate = new DocumentationScaffoldRequestGate();
	const output = vscode.window.createOutputChannel('Sushi Documentation');
	context.subscriptions.push(output);

	context.subscriptions.push(vscode.commands.registerCommand('type', async (args: { readonly text?: string } | undefined) => {
		const beforeEditor = vscode.window.activeTextEditor;
		const beforeSelection = beforeEditor?.selection;
		const beforeLine = beforeEditor && beforeSelection
			? beforeEditor.document.lineAt(beforeSelection.active.line).text
			: '';
		const beforeCharacter = beforeSelection?.active.character ?? -1;
		const beforeVersion = beforeEditor?.document.version;
		await vscode.commands.executeCommand('default:type', args);

		if (args?.text !== '/' || !beforeEditor || !beforeSelection || beforeVersion === undefined
			|| !beforeSelection.isEmpty || beforeEditor.document.languageId !== 'sushi'
			|| beforeEditor.document.uri.scheme !== 'file' || !client.isRunning()) {
			return;
		}

		const editor = vscode.window.activeTextEditor;
		if (!editor || editor.document.uri.toString() !== beforeEditor.document.uri.toString()
			|| editor.document.version !== beforeVersion + 1 || editor.selection.active.line !== beforeSelection.active.line
			|| !editor.selection.isEmpty) {
			return;
		}

		const lineNumber = editor.selection.active.line;
		const triggerCharacter = editor.selection.active.character;
		const line = editor.document.lineAt(lineNumber).text;
		const trigger = identifyThirdSlashTyping(
			args.text, beforeLine, beforeCharacter, line, triggerCharacter, beforeSelection.isEmpty);
		if (!trigger) {
			return;
		}

		const documentUri = editor.document.uri.toString();
		const documentVersion = editor.document.version;
		if (!requestGate.shouldHandle(documentUri, documentVersion, lineNumber)) {
			return;
		}

		try {
			const response = await client.sendRequest<DocumentationScaffoldResponse>('sourceDocumentation/scaffold', {
				documentUri, documentVersion, line: lineNumber, character: triggerCharacter
			});
			const currentEditor = vscode.window.activeTextEditor;
			if (!response.eligible || response.documentVersion !== documentVersion || !currentEditor
				|| !isScaffoldResponseCurrent(documentUri, documentVersion, currentEditor.document.uri.toString(), currentEditor.document.version)
				|| currentEditor.selection.active.line !== lineNumber
				|| currentEditor.selection.active.character !== triggerCharacter
				|| !currentEditor.selection.isEmpty) {
				return;
			}

			const snippet = new vscode.SnippetString(createDocumentationScaffoldSnippet(
				response.parameterNames, response.hasReturnDescription,
				currentEditor.document.eol === vscode.EndOfLine.CRLF ? '\r\n' : '\n'));
			await currentEditor.insertSnippet(snippet, new vscode.Range(lineNumber, trigger.slashColumn, lineNumber, triggerCharacter));
		} catch (error) {
			output.appendLine(`Documentation scaffold request failed: ${String(error)}`);
		}
	}));
}
