import test from 'node:test';
import assert from 'node:assert/strict';
import Module, { createRequire } from 'node:module';
import { URL } from 'node:url';

const load = createRequire(import.meta.url);

function harness(options = {}) {
	const commands = new Map();
	const notifications = [];
	const requests = [];
	const shown = [];
	const openDocuments = [];
	const messages = [];
	const definitions = [];
	let close;
	let provider;
	const uri = value => ({ scheme: new URL(value).protocol.slice(0, -1), fsPath: new URL(value).pathname, toString: () => value });
	const disposable = { dispose() {} };
	const source = { document: { languageId: 'sushi', uri: uri('file:///Source.sus'), version: 7 }, selection: { active: { line: 1, character: 9 } } };
	const reply = { succeeded: true, artifactId: 'one', documentUri: 'sushi-inspect://session/one/Value.structuredIR',
		stage: 'structuredIR', text: 'exact compiler text\n', functionLabel: 'Value', sourceUri: 'file:///Source.sus', sourceVersion: 7 };
	const vscode = {
		Uri: { parse: uri },
		ProgressLocation: { Notification: 1 }, ViewColumn: { Beside: 2 },
		commands: { registerCommand(id, action) { commands.set(id, action); return disposable; } },
		workspace: {
			textDocuments: openDocuments,
			registerTextDocumentContentProvider(scheme, content) { assert.equal(scheme, 'sushi-inspect'); provider = content; return disposable; },
			onDidCloseTextDocument(listener) { close = listener; return disposable; },
			async openTextDocument(documentUri) { const document = { uri: documentUri, languageId: 'plaintext', text: provider.provideTextDocumentContent(documentUri) }; openDocuments.push(document); return document; }
		},
		Position: class Position { constructor(line, character) { this.line = line; this.character = character; } },
		Range: class Range { constructor(startLine, startCharacter, endLine, endCharacter) { this.start = { line: startLine, character: startCharacter }; this.end = { line: endLine, character: endCharacter }; } },
		languages: {
			registerDefinitionProvider(selector, providerDefinition) { definitions.push({ selector, provider: providerDefinition }); return disposable; },
			async setTextDocumentLanguage(document, language) { close(document); const updated = { ...document, languageId: language }; openDocuments.splice(openDocuments.indexOf(document), 1, updated); return updated; }
		},
		window: {
			activeTextEditor: options.noEditor ? undefined : source,
			createOutputChannel() { return { appendLine() {}, dispose() {} }; },
			async showInformationMessage(message) { messages.push(message); },
			async showErrorMessage(message) { messages.push(message); },
			async withProgress(_settings, action) { return action({}, { isCancellationRequested: options.cancelled ?? false }); },
			async showQuickPick(items) { return items[1]; },
			async showTextDocument(document, settings) { assert.equal(settings.viewColumn, 2); assert.equal(settings.preview, false); shown.push(document); }
		}
	};
	const client = {
		isRunning: () => !options.disconnected,
		async sendNotification(method, params) { notifications.push({ method, params }); },
		async sendRequest(method, params) {
			requests.push({ method, params });
			if (method === 'sourceInspection/definition') {
				return options.definitionReply;
			}
			if (options.ambiguous && !params.projectUri) {
				return { succeeded: false, error: 'Choose project', projects: ['file:///First.susproj', 'file:///Second.susproj'] };
			}
			return options.failure ? { succeeded: false, error: 'Select a package function', projects: [], diagnostics: [] } : reply;
		}
	};
	const originalLoad = Module._load;
	const key = load.resolve('../out/inspection.js');
	delete load.cache[key];
	Module._load = function (name, ...args) {
		return name === 'vscode' ? vscode : originalLoad.call(this, name, ...args);
	};
	try {
		const { registerInspection } = load('../out/inspection.js');
		registerInspection({ subscriptions: [] }, client);
	} finally {
		Module._load = originalLoad;
	}
	return { commands, notifications, requests, shown, messages, definitions, openDocuments, source, reply, close: document => { openDocuments.splice(openDocuments.indexOf(document), 1); close(document); }, changeLanguage: document => vscode.languages.setTextDocumentLanguage(document, 'plaintext'), read: value => provider.provideTextDocumentContent(uri(value ?? reply.documentUri)) };
}

test('command sends URI version position stage without source text and opens frozen content beside source', async () => {
	const h = harness();
	await h.commands.get('sushi.showStructuredIR')();
	assert.equal(h.requests[0].method, 'sourceInspection/inspectFunction');
	assert.deepEqual(h.requests[0].params, { sourceUri: 'file:///Source.sus', sourceVersion: 7, position: { line: 1, character: 9 }, stage: 'structuredIR', projectUri: undefined });
	assert.equal(h.shown[0].languageId, 'sushi-structured-ir');
	assert.equal(h.shown[0].text, h.reply.text);
	assert.equal(h.source.document.languageId, 'sushi');
	assert.equal(h.read(), h.reply.text);
	assert.equal(h.notifications.length, 0, 'language-change close must not release metadata');
	h.close(h.shown[0]);
	await Promise.resolve();
	assert.deepEqual(h.notifications[0], { method: 'sourceInspection/release', params: { artifactId: 'one' } });
	assert.throws(h.read, /no longer available/);
});

test('lowered command uses the separate stage and language', async () => {
	const h = harness();
	h.reply.stage = 'loweredIR';
	h.reply.documentUri = 'sushi-inspect://session/one/Value.loweredIR';
	await h.commands.get('sushi.showLoweredIR')();
	assert.equal(h.requests[0].params.stage, 'loweredIR');
	assert.equal(h.shown[0].languageId, 'sushi-lowered-ir');
});

test('ambiguous project response requires explicit user choice', async () => {
	const h = harness({ ambiguous: true });
	await h.commands.get('sushi.showStructuredIR')();
	assert.equal(h.requests[1].params.projectUri, 'file:///Second.susproj');
	assert.equal(h.shown.length, 1);
});

for (const options of [{ noEditor: true }, { disconnected: true }, { failure: true }]) {
	test(`unavailable inspection has a message and opens no document: ${JSON.stringify(options)}`, async () => {
		const h = harness(options);
		await h.commands.get('sushi.showStructuredIR')();
		assert.equal(h.shown.length, 0);
		assert.equal(h.messages.length, 1);
	});
}

test('cancellation after a completed reply releases the artifact without opening a document', async () => {
	const h = harness({ cancelled: true });
	await h.commands.get('sushi.showStructuredIR')();
	assert.equal(h.shown.length, 0);
	assert.equal(h.messages.length, 0);
	assert.equal(h.notifications[0].params.artifactId, 'one');
});

for (const language of ['sushi-structured-ir', 'susproj', 'typescript']) {
	test(`generated and unrelated editors cannot issue inspection requests: ${language}`, async () => {
		const h = harness();
		h.source.document.languageId = language;
		await h.commands.get('sushi.showStructuredIR')();
		assert.equal(h.requests.length, 0);
		assert.equal(h.shown.length, 0);
		assert.equal(h.messages.length, 1);
	});
}


test('a later user language change retains the frozen document until its actual close', async () => {
	const h = harness();
	await h.commands.get('sushi.showStructuredIR')();
	const updated = await h.changeLanguage(h.shown[0]);
	await Promise.resolve();
	assert.equal(h.notifications.length, 0);
	assert.equal(h.read(), h.reply.text);
	h.close(updated);
	await Promise.resolve();
	assert.equal(h.notifications[0].params.artifactId, 'one');
});

test('native IR definition providers register lazy target content before returning a location link', async () => {
	const targetUri = 'sushi-inspect://session/two/Helper.structuredIR';
	const h = harness({ definitionReply: {
		found: true, targetDocumentUri: targetUri, targetArtifactId: 'two', stage: 'structuredIR', generated: true,
		text: 'public function int32 Helper(int32 input)\n', functionLabel: 'Helper',
		originRange: { start: { line: 2, character: 7 }, end: { line: 2, character: 10 } },
		targetRange: { start: { line: 0, character: 25 }, end: { line: 0, character: 31 } }
	} });
	assert.deepEqual(h.definitions.map(item => item.selector), [
		{ language: 'sushi-structured-ir', scheme: 'sushi-inspect' },
		{ language: 'sushi-lowered-ir', scheme: 'sushi-inspect' }
	]);
	await h.commands.get('sushi.showStructuredIR')();
	const sourceDocument = h.shown[0];
	const location = await h.definitions[0].provider.provideDefinition(sourceDocument, { line: 2, character: 8 }, { isCancellationRequested: false });
	assert.equal(h.requests[1].method, 'sourceInspection/definition');
	assert.equal(h.requests[1].params.artifactId, 'one');
	assert.equal(location[0].targetUri.toString(), targetUri);
	assert.deepEqual(location[0].targetSelectionRange.start, { line: 0, character: 25 });
	const target = h.openDocuments.find(document => document.uri.toString() === targetUri);
	assert.equal(target.text, h.read(targetUri));
	assert.equal(h.read(), h.reply.text);
	assert.equal(h.definitions.length, 2);
	assert.equal(h.requests[1].params.documentUri, h.reply.documentUri);
	assert.equal(target.languageId, 'sushi-structured-ir');
});
