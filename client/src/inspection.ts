import * as path from 'path';
import * as vscode from 'vscode';
import { LanguageClient } from 'vscode-languageclient/node';
import { InspectionDefinitionResponse, InspectionDocuments, InspectionResponse, InspectionStage, InspectionSuccess } from './inspectionDocuments';

/** Registers commands and a content provider; generated text is never inserted into source or an untitled editor. */
export function registerInspection(context: vscode.ExtensionContext, client: LanguageClient): void {
	const documents = new InspectionDocuments();
	const changingLanguage = new Set<string>();
	const output = vscode.window.createOutputChannel('Sushi Inspection');
	const release = async (artifactId: string): Promise<void> => {
		try {
			await client.sendNotification('sourceInspection/release', { artifactId });
		} catch (error) {
			output.appendLine(`Could not release inspection artifact: ${String(error)}`);
		}
	};

	context.subscriptions.push(
		output,
		{ dispose: () => documents.clear() },
		vscode.workspace.registerTextDocumentContentProvider('sushi-inspect', {
			provideTextDocumentContent: uri => documents.read(uri.toString())
		}),
		vscode.languages.registerDefinitionProvider(
			{ language: 'sushi-structured-ir', scheme: 'sushi-inspect' },
			{ provideDefinition: (document, position, token) => provideDefinition(document, position, token) }
		),
		vscode.languages.registerDefinitionProvider(
			{ language: 'sushi-lowered-ir', scheme: 'sushi-inspect' },
			{ provideDefinition: (document, position, token) => provideDefinition(document, position, token) }
		),
		vscode.workspace.onDidCloseTextDocument(document => {
			if (document.uri.scheme === 'sushi-inspect' && !changingLanguage.has(document.uri.toString())) {
				// Language changes also emit close/open pairs. Check the settled document set before releasing ownership.
				queueMicrotask(() => {
					const stillOpen = vscode.workspace.textDocuments.some(item => item.uri.toString() === document.uri.toString());
					if (!stillOpen) {
						const id = documents.remove(document.uri.toString());
						if (id) {
							void release(id);
						}
					}
				});
			}
		}),
		vscode.commands.registerCommand('sushi.showStructuredIR', () => inspect('structuredIR')),
		vscode.commands.registerCommand('sushi.showLoweredIR', () => inspect('loweredIR'))
	);

	async function provideDefinition(document: vscode.TextDocument, position: vscode.Position, token: vscode.CancellationToken): Promise<vscode.DefinitionLink[] | undefined> {
		if (document.uri.scheme !== 'sushi-inspect' || !client.isRunning()) {
			return undefined;
		}
		const origin = documents.get(document.uri.toString());
		if (!origin || token.isCancellationRequested) {
			return undefined;
		}
		let generatedTarget: { readonly uri: string; readonly artifactId: string } | undefined;
		try {
			const response = await client.sendRequest<InspectionDefinitionResponse>('sourceInspection/definition', {
				documentUri: document.uri.toString(), artifactId: origin.artifactId,
				position: { line: position.line, character: position.character }
			}, token);
			if (!response.found || !response.targetDocumentUri || !response.targetRange) {
				if (response.error) {
					output.appendLine(`Definition navigation failed: ${response.error}`);
				}
				return undefined;
			}
			if (token.isCancellationRequested) {
				if (response.generated && response.targetDocumentUri && response.targetArtifactId) {
					await release(response.targetArtifactId);
				}
				return undefined;
			}

			let target = documents.get(response.targetDocumentUri);
			if (!target && response.text !== undefined && response.targetArtifactId && response.stage) {
				if (response.generated) {
					generatedTarget = { uri: response.targetDocumentUri, artifactId: response.targetArtifactId };
				}
				const content: InspectionSuccess = {
					succeeded: true, artifactId: response.targetArtifactId, documentUri: response.targetDocumentUri,
					stage: response.stage, text: response.text, functionLabel: response.functionLabel ?? '', sourceUri: '', sourceVersion: null
				};
				documents.add(content);
				target = documents.get(response.targetDocumentUri);
			}
			if (!target || !response.targetArtifactId || !response.stage || target.artifactId !== response.targetArtifactId) {
				if (generatedTarget) {
					documents.remove(generatedTarget.uri);
					await release(generatedTarget.artifactId);
				}
				output.appendLine('Definition navigation returned a target that is not available in the inspection document provider.');
				return undefined;
			}

			const uri = vscode.Uri.parse(response.targetDocumentUri);
			let targetDocument = await vscode.workspace.openTextDocument(uri);
			const language = response.stage === 'structuredIR' ? 'sushi-structured-ir' : 'sushi-lowered-ir';
			if (targetDocument.languageId !== language) {
				changingLanguage.add(uri.toString());
				try {
					targetDocument = await vscode.languages.setTextDocumentLanguage(targetDocument, language);
				} finally {
					changingLanguage.delete(uri.toString());
				}
			}
			const targetRange = toRange(response.targetRange);
			const originRange = response.originRange ? toRange(response.originRange) : new vscode.Range(position, position);
			if (token.isCancellationRequested) {
				if (generatedTarget) {
					documents.remove(generatedTarget.uri);
					await release(generatedTarget.artifactId);
				}
				return undefined;
			}
			return [{
				originSelectionRange: originRange, targetUri: uri, targetRange, targetSelectionRange: targetRange
			}];
		} catch (error) {
			if (generatedTarget) {
				documents.remove(generatedTarget.uri);
				await release(generatedTarget.artifactId);
			}
			if (!token.isCancellationRequested) {
				output.appendLine(`Definition navigation failed: ${String(error)}`);
			}
			return undefined;
		}
	}

	function toRange(range: NonNullable<InspectionDefinitionResponse['targetRange']>): vscode.Range {
		return new vscode.Range(range.start.line, range.start.character, range.end.line, range.end.character);
	}

	async function inspect(stage: InspectionStage): Promise<void> {
		const editor = vscode.window.activeTextEditor;
		if (!editor || editor.document.languageId !== 'sushi' || editor.document.uri.scheme !== 'file'
			|| path.extname(editor.document.uri.fsPath).toLowerCase() !== '.sus') {
			await vscode.window.showInformationMessage('Open a Sushi source file and place the cursor inside a package function.');
			return;
		}
		if (!client.isRunning()) {
			await vscode.window.showErrorMessage('The Sushi language server is not connected. Start or reconnect the server and retry.');
			return;
		}

		// Capture selection and version once; a later edit must not silently change the requested source.
		const sourceUri = editor.document.uri.toString();
		const sourceVersion = editor.document.version;
		const position = { line: editor.selection.active.line, character: editor.selection.active.character };
		await vscode.window.withProgress({
			location: vscode.ProgressLocation.Notification,
			title: stage === 'structuredIR' ? 'Sushi: Show Structured IR' : 'Sushi: Show Lowered IR',
			cancellable: true
		}, async (_progress, token) => {
			let artifact: InspectionSuccess | undefined;
			try {
				let projectUri: string | undefined;
				let response: InspectionResponse;
				while (true) {
					response = await client.sendRequest<InspectionResponse>('sourceInspection/inspectFunction', {
						sourceUri, sourceVersion, position, stage, projectUri
					}, token);
					if (response.succeeded || !response.projects?.length) {
						break;
					}
					const selected = await vscode.window.showQuickPick(response.projects.map(uri => ({
						label: path.basename(vscode.Uri.parse(uri).fsPath),
						description: vscode.Uri.parse(uri).fsPath,
						uri
					})), { placeHolder: 'Choose the Sushi project for this source file' }, token);
					if (!selected || token.isCancellationRequested) {
						return;
					}
					projectUri = selected.uri;
				}

				if (!response.succeeded) {
					if (!token.isCancellationRequested) {
						const diagnostic = response.diagnostics?.[0];
						const detail = diagnostic ? ` ${diagnostic.code}: ${diagnostic.message} (${diagnostic.line + 1}:${diagnostic.character + 1})` : '';
						await vscode.window.showErrorMessage(response.error + detail);
					}
					return;
				}
				artifact = response;
				if (token.isCancellationRequested) {
					await release(artifact.artifactId);
					return;
				}

				const uri = vscode.Uri.parse(artifact.documentUri);
				documents.add({ ...artifact, documentUri: uri.toString() });
				let document = await vscode.workspace.openTextDocument(uri);
				const language = artifact.stage === 'structuredIR' ? 'sushi-structured-ir' : 'sushi-lowered-ir';
				if (document.languageId !== language) {
					// VS Code emits close/open events during language changes; this is not a document disposal.
					changingLanguage.add(uri.toString());
					try {
						document = await vscode.languages.setTextDocumentLanguage(document, language);
					} finally {
						changingLanguage.delete(uri.toString());
					}
				}
				await vscode.window.showTextDocument(document, { viewColumn: vscode.ViewColumn.Beside, preview: false });
			} catch (error) {
				if (artifact) {
					documents.remove(vscode.Uri.parse(artifact.documentUri).toString());
					await release(artifact.artifactId);
				}
				if (!token.isCancellationRequested) {
					output.appendLine(`Inspection failed: ${String(error)}`);
					await vscode.window.showErrorMessage('Sushi inspection failed. Check the language-server connection and the Sushi Inspection output.');
				}
			}
		});
	}
}
