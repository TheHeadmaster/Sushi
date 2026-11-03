import test from 'node:test';
import assert from 'node:assert/strict';
import Module, { createRequire } from 'node:module';
import { URL } from 'node:url';

const load = createRequire(import.meta.url);

async function withManualTimers(action) {
	const originalSetTimeout = globalThis.setTimeout;
	const originalClearTimeout = globalThis.clearTimeout;
	let now = 0;
	let nextId = 0;
	const timers = new Map();
	const history = [];
	globalThis.setTimeout = (callback, delay, ...args) => {
		const id = ++nextId;
		const timer = { id, callback: () => callback(...args), due: now + delay };
		timers.set(id, timer);
		history.push({ id, delay, cleared: false });
		return id;
	};
	globalThis.clearTimeout = id => {
		timers.delete(id);
		const timer = history.find(item => item.id === id);
		if (timer) { timer.cleared = true; }
	};
	const clock = {
		advanceBy(milliseconds) {
			now += milliseconds;
			for (const timer of [...timers.values()].filter(item => item.due <= now).sort((left, right) => left.due - right.due)) {
				timers.delete(timer.id);
				timer.callback();
			}
		},
		get pendingCount() { return timers.size; },
		history
	};
	try {
		return await action(clock);
	} finally {
		globalThis.setTimeout = originalSetTimeout;
		globalThis.clearTimeout = originalClearTimeout;
	}
}

function deferred() {
	let resolve;
	let reject;
	const promise = new Promise((resolvePromise, rejectPromise) => { resolve = resolvePromise; reject = rejectPromise; });
	return { promise, resolve, reject };
}

function harness(options = {}) {
	const commands = new Map();
	const notifications = [];
	const requests = [];
	const shown = [];
	const openDocuments = [];
	const visibleEditors = [];
	const tabGroups = [{ viewColumn: 1, tabs: [] }];
	const editors = new Map();
	const messages = [];
	const errorProgressStates = [];
	const progressCalls = [];
	const cancellationSources = [];
	const quickPicks = [];
	const definitions = [];
	const hovers = [];
	const providers = new Map();
	let close;
	let resolveErrorShown;
	const errorShown = new Promise(resolve => { resolveErrorShown = resolve; });
	let activeProgress = 0;
	let requestHandler = options.requestHandler;
	class CancellationTokenSource {
		constructor() {
			let cancelled = false;
			const listeners = new Set();
			this.disposed = false;
			this.token = {
				get isCancellationRequested() { return cancelled; },
				onCancellationRequested(listener) {
					if (cancelled) { Promise.resolve().then(listener); }
					listeners.add(listener);
					return { dispose() { listeners.delete(listener); } };
				}
			};
			this.listenerCount = () => listeners.size;
			this.cancel = () => {
				if (cancelled) { return; }
				cancelled = true;
				for (const listener of [...listeners]) { listener(); }
			};
			cancellationSources.push(this);
		}
		dispose() { this.disposed = true; }
	}
	const uri = value => ({ scheme: new URL(value).protocol.slice(0, -1), fsPath: new URL(value).pathname, toString: () => value });
	class TabInputText { constructor(documentUri) { this.uri = documentUri; } }
	const disposable = { dispose() {} };
	const sourceDocument = { languageId: 'sushi', uri: uri('file:///Source.sus'), version: 7,
		text: options.sourceText ?? 'public int32 Value() { return 1; }', getText() { return this.text; } };
	const source = { document: sourceDocument, selection: { active: { line: 1, character: 9 } }, viewColumn: 1, revealRange() {} };
	if (options.sourceTab !== false) {
		openDocuments.push(sourceDocument);
		if (!options.sourceTabInactive) { visibleEditors.push(source); }
		tabGroups[0].tabs.push({ input: new TabInputText(sourceDocument.uri) });
		editors.set(sourceDocument.uri.toString(), source);
	}
	let activeTextEditor = source;
	const reply = { succeeded: true, artifactId: 'one', documentUri: 'sushi-inspect://session/one/Value.structuredIR',
		stage: 'structuredIR', text: 'exact compiler text\n', functionLabel: 'Value', sourceUri: 'file:///Source.sus', sourceVersion: 7 };
	const vscode = {
		Uri: { parse: uri, from({ scheme, path }) { return uri(`${scheme}:${path}`); } },
		TabInputText,
		CancellationTokenSource,
		ProgressLocation: { Notification: 1 }, ViewColumn: { Beside: 2 },
		commands: { registerCommand(id, action) { commands.set(id, action); return disposable; } },
		workspace: {
			textDocuments: openDocuments,
			fs: { async stat() { if (options.sourceUnavailable) { throw new Error('File is unavailable'); } return {}; } },
			registerTextDocumentContentProvider(scheme, content) { providers.set(scheme, content); return disposable; },
			onDidCloseTextDocument(listener) { close = listener; return disposable; },
			async openTextDocument(documentUri) {
				if (options.openFailure) { throw new Error('Failed to open virtual document'); }
				const existing = openDocuments.find(document => document.uri.toString() === documentUri.toString());
				if (existing) { return existing; }
				if (documentUri.scheme === 'file') {
					const document = { uri: documentUri, languageId: 'sushi', text: options.sourceText ?? sourceDocument.text, version: options.sourceVersion ?? 7,
						getText() { return this.text; } };
					openDocuments.push(document);
					return document;
				}
				const document = { uri: documentUri, languageId: 'plaintext', text: providers.get(documentUri.scheme).provideTextDocumentContent(documentUri),
					getText() { return this.text; } };
				openDocuments.push(document);
				return document;
			}
		},
		Position: class Position { constructor(line, character) { this.line = line; this.character = character; } },
		Selection: class Selection { constructor(startLine, startCharacter, endLine, endCharacter) { this.start = { line: startLine, character: startCharacter }; this.end = { line: endLine, character: endCharacter }; } },
		TextEditorRevealType: { InCenter: 1 },
		Range: class Range { constructor(startLine, startCharacter, endLine, endCharacter) { this.start = { line: startLine, character: startCharacter }; this.end = { line: endLine, character: endCharacter }; } },
		MarkdownString: class MarkdownString {
			value = [];
			appendCodeblock(value, language) { this.value.push({ kind: 'code', value, language }); return this; }
			appendText(value) { this.value.push({ kind: 'text', value }); return this; }
		},
		Hover: class Hover { constructor(contents, range) { this.contents = contents; this.range = range; } },
		languages: {
			registerDefinitionProvider(selector, providerDefinition) { definitions.push({ selector, provider: providerDefinition }); return disposable; },
			registerHoverProvider(selector, providerHover) { hovers.push({ selector, provider: providerHover }); return disposable; },
			async setTextDocumentLanguage(document, language) {
				close(document);
				const updated = { ...document, languageId: language };
				openDocuments.splice(openDocuments.indexOf(document), 1, updated);
				const editor = editors.get(document.uri.toString());
				if (editor) { editor.document = updated; }
				return updated;
			}
		},
		window: {
			get activeTextEditor() { return options.noEditor ? undefined : activeTextEditor; },
			get visibleTextEditors() { return visibleEditors; },
			tabGroups: { get all() { return tabGroups; } },
			createOutputChannel() { return { appendLine() {}, dispose() {} }; },
			async showInformationMessage(message) { messages.push(message); },
			showErrorMessage(message) { messages.push(message); errorProgressStates.push(activeProgress === 0); resolveErrorShown(); return options.errorPromise ?? Promise.resolve(); },
			withProgress(settings, action) {
				const source = new CancellationTokenSource();
				const call = { settings, source, settled: false };
				progressCalls.push(call);
				activeProgress++;
				return Promise.resolve().then(() => action({}, source.token)).finally(() => {
					call.settled = true;
					activeProgress--;
				});
			},
			async showQuickPick(items) { quickPicks.push(items); return options.cancelQuickPick ? undefined : items[options.quickPickIndex ?? 1]; },
			async showTextDocument(document, settings) {
				assert.ok(settings.viewColumn); assert.equal(settings.preview, false); shown.push(document);
				const key = document.uri.toString();
				let editor = editors.get(key);
				if (!editor) {
					editor = { document, selection: { active: { line: 0, character: 0 } }, revealRange() {}, viewColumn: settings.viewColumn };
					editors.set(key, editor);
					const group = tabGroups.find(item => item.viewColumn === settings.viewColumn) ?? { viewColumn: settings.viewColumn, tabs: [] };
					if (!tabGroups.includes(group)) { tabGroups.push(group); }
					group.tabs.push({ input: new vscode.TabInputText(document.uri) });
					visibleEditors.push(editor);
				} else {
					editor.viewColumn = settings.viewColumn;
					if (!visibleEditors.includes(editor)) { visibleEditors.push(editor); }
				}
				activeTextEditor = editor;
				return activeTextEditor;
			}
		}
	};
	const client = {
		isRunning: () => !options.disconnected,
		async sendNotification(method, params) { notifications.push({ method, params }); },
		sendRequest(method, params, token) {
			requests.push(token === undefined ? { method, params } : { method, params, token });
			if (requestHandler) { return requestHandler(method, params, token); }
			if (method === 'sourceInspection/definition') {
				return Promise.resolve(options.definitionReply);
			}
			if (method === 'sourceInspection/sourceDefinition') {
				return Promise.resolve(options.sourceDefinitionReply);
			}
			if (method === 'sourceInspection/hover') {
				return Promise.resolve(options.hoverReply);
			}
			if (options.throwRequest) { return Promise.reject(new Error('Unexpected inspection request failure')); }
			if (options.ambiguous && !params.projectUri) {
				return Promise.resolve({ succeeded: false, error: 'Choose project', projects: ['file:///First.susproj', 'file:///Second.susproj'] });
			}
			return Promise.resolve(options.failure ? { succeeded: false, error: options.failureText ?? 'Select a package function', projects: [], diagnostics: [] } : reply);
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
	return { commands, notifications, requests, shown, messages, errorProgressStates, errorShown, definitions, hovers, openDocuments, source, sourceDocument, reply,
		progressCalls, cancellationSources, quickPicks,
		tabGroups, visibleEditors,
		get activeEditor() { return activeTextEditor; },
		close: document => {
			openDocuments.splice(openDocuments.indexOf(document), 1);
			const key = document.uri.toString();
			for (const group of tabGroups) { group.tabs = group.tabs.filter(tab => tab.input.uri.toString() !== key); }
			visibleEditors.splice(0, visibleEditors.length, ...visibleEditors.filter(editor => editor.document.uri.toString() !== key));
			editors.delete(key);
			close(document);
		},
		activate: document => { activeTextEditor = editors.get(document.uri.toString()); },
		setSourceDefinitionReply: value => { options.sourceDefinitionReply = value; },
		setRequestHandler: value => { requestHandler = value; },
		cancelProgress: index => progressCalls[index].source.cancel(),
		tabCount: () => tabGroups.reduce((count, group) => count + group.tabs.length, 0),
		changeLanguage: document => vscode.languages.setTextDocumentLanguage(document, 'plaintext'),
		read: value => { const documentUri = uri(value ?? reply.documentUri); return providers.get(documentUri.scheme).provideTextDocumentContent(documentUri); } };
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
	assert.equal(h.progressCalls.length, 0, 'a fast inspection must not flash progress');
	h.close(h.shown[0]);
	await Promise.resolve();
	assert.deepEqual(h.notifications[0], { method: 'sourceInspection/release', params: { artifactId: 'one' } });
	assert.throws(h.read, /no longer available/);
});

test('fast inspection opens immediately and clears its delayed progress timer', async () => {
	await withManualTimers(async clock => {
		const h = harness();
		await h.commands.get('sushi.showStructuredIR')();
		assert.equal(h.requests.length, 1);
		assert.equal(h.progressCalls.length, 0);
		assert.equal(h.shown.length, 1);
		assert.equal(clock.pendingCount, 0);
		assert.deepEqual(clock.history.map(timer => ({ delay: timer.delay, cleared: timer.cleared })), [{ delay: 500, cleared: true }]);
	});
});

test('slow inspection starts immediately and displays cancellable progress at 500 ms', async () => {
	await withManualTimers(async clock => {
		const operation = deferred();
		const h = harness({ requestHandler: () => operation.promise });
		const running = h.commands.get('sushi.showLoweredIR')();
		assert.equal(h.requests.length, 1, 'the language-server request starts before progress');
		assert.equal(h.progressCalls.length, 0);
		clock.advanceBy(499);
		assert.equal(h.progressCalls.length, 0);
		clock.advanceBy(1);
		assert.equal(h.progressCalls.length, 1);
		assert.equal(h.progressCalls[0].settings.cancellable, true);
		assert.match(h.progressCalls[0].settings.title, /Lowered IR/);
		operation.resolve({ ...h.reply, stage: 'loweredIR', documentUri: 'sushi-inspect://session/one/Value.loweredIR' });
		await running;
		assert.equal(h.shown[0].languageId, 'sushi-lowered-ir');
		assert.equal(h.progressCalls[0].settled, true);
		assert.equal(clock.pendingCount, 0);
	});
});

test('completion observed at the 500 ms boundary prevents progress and runs once', async () => {
	await withManualTimers(async clock => {
		const operation = deferred();
		let requestCount = 0;
		const h = harness({ requestHandler: () => { requestCount++; return operation.promise; } });
		const running = h.commands.get('sushi.showStructuredIR')();
		operation.resolve(h.reply);
		await Promise.resolve();
		await Promise.resolve();
		clock.advanceBy(500);
		await running;
		assert.equal(requestCount, 1);
		assert.equal(h.progressCalls.length, 0);
		assert.equal(h.shown.length, 1);
		assert.equal(clock.pendingCount, 0);
	});
});

test('repeated inspection focuses the existing exact-snapshot IR tab without allocating another tab', async () => {
	const h = harness();
	await h.commands.get('sushi.showStructuredIR')();
	const tabCount = h.tabCount();
	h.activate(h.sourceDocument);
	await h.commands.get('sushi.showStructuredIR')();
	assert.equal(h.tabCount(), tabCount);
	assert.equal(h.shown[0], h.shown[1]);
	assert.equal(h.requests.length, 2);
	assert.deepEqual(h.notifications, []);
	assert.equal(h.progressCalls.length, 0, 'exact-snapshot artifact reuse stays on the fast path');
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
	assert.equal(h.quickPicks.length, 1);
	assert.equal(h.progressCalls.length, 0, 'fast requests and project selection stay free of progress overlays');
});

test('project selection is outside progress and each selected-project request gets its own delay', async () => {
	await withManualTimers(async clock => {
		const secondRequest = deferred();
		const h = harness({ requestHandler: (_method, params) => params.projectUri
			? secondRequest.promise
			: Promise.resolve({ succeeded: false, error: 'Choose project', projects: ['file:///First.susproj', 'file:///Second.susproj'] }) });
		const running = h.commands.get('sushi.showStructuredIR')();
		for (let attempt = 0; attempt < 12 && h.requests.length < 2; attempt++) {
			await Promise.resolve();
		}
		assert.equal(h.quickPicks.length, 1);
		assert.equal(h.requests.length, 2);
		assert.equal(h.progressCalls.length, 0);
		clock.advanceBy(499);
		assert.equal(h.progressCalls.length, 0);
		clock.advanceBy(1);
		assert.equal(h.progressCalls.length, 1);
		secondRequest.resolve(h.reply);
		await running;
		assert.equal(h.shown.length, 1);
	});
});

test('canceling project selection exits without another request or progress notification', async () => {
	const h = harness({ ambiguous: true, cancelQuickPick: true });
	await h.commands.get('sushi.showStructuredIR')();
	assert.equal(h.requests.length, 1);
	assert.equal(h.quickPicks.length, 1);
	assert.equal(h.progressCalls.length, 0);
	assert.equal(h.messages.length, 0);
	assert.equal(h.shown.length, 0);
});

for (const options of [{ noEditor: true }, { disconnected: true }, { failure: true }]) {
	test(`unavailable inspection has a message and opens no document: ${JSON.stringify(options)}`, async () => {
		const h = harness(options);
		await h.commands.get('sushi.showStructuredIR')();
		assert.equal(h.shown.length, 0);
		assert.equal(h.messages.length, 1);
	});
}

test('canceling visible progress cancels the request and releases a late new artifact', async () => {
	await withManualTimers(async clock => {
		const operation = deferred();
		const h = harness({ requestHandler: () => operation.promise });
		const running = h.commands.get('sushi.showStructuredIR')();
		const requestToken = h.requests[0].token;
		clock.advanceBy(500);
		assert.equal(h.progressCalls.length, 1);
		h.cancelProgress(0);
		await Promise.resolve();
		assert.equal(requestToken.isCancellationRequested, true);
		operation.resolve(h.reply);
		await running;
		assert.equal(h.shown.length, 0);
		assert.equal(h.messages.length, 0);
		assert.deepEqual(h.notifications, [{ method: 'sourceInspection/release', params: { artifactId: 'one' } }]);
		const requestSource = h.cancellationSources.find(source => source.token === requestToken);
		assert.equal(requestSource.disposed, true);
		assert.equal(requestSource.listenerCount(), 0);
		assert.equal(clock.pendingCount, 0);
	});
});

test('canceling a repeated request does not release its already-open reused artifact', async () => {
	await withManualTimers(async clock => {
		const secondRequest = deferred();
		let requestCount = 0;
		const h = harness({ requestHandler: () => ++requestCount === 1 ? Promise.resolve(h.reply) : secondRequest.promise });
		await h.commands.get('sushi.showStructuredIR')();
		h.activate(h.sourceDocument);
		const running = h.commands.get('sushi.showStructuredIR')();
		clock.advanceBy(500);
		assert.equal(h.progressCalls.length, 1);
		h.cancelProgress(0);
		secondRequest.resolve(h.reply);
		await running;
		assert.equal(h.shown.length, 1);
		assert.equal(h.tabCount(), 2);
		assert.equal(h.notifications.length, 0);
	});
});

test('independent inspections keep their timers and cancellation tokens separate', async () => {
	await withManualTimers(async clock => {
		const firstRequest = deferred();
		const secondRequest = deferred();
		let requestCount = 0;
		const h = harness({ requestHandler: () => ++requestCount === 1 ? firstRequest.promise : secondRequest.promise });
		const first = h.commands.get('sushi.showStructuredIR')();
		const second = h.commands.get('sushi.showLoweredIR')();
		assert.equal(h.requests.length, 2);
		clock.advanceBy(500);
		assert.equal(h.progressCalls.length, 2);
		h.cancelProgress(0);
		await Promise.resolve();
		assert.equal(h.requests[0].token.isCancellationRequested, true);
		assert.equal(h.requests[1].token.isCancellationRequested, false);
		firstRequest.resolve(h.reply);
		secondRequest.resolve({ ...h.reply, artifactId: 'two', documentUri: 'sushi-inspect://session/two/Value.loweredIR', stage: 'loweredIR' });
		await Promise.all([first, second]);
		assert.equal(h.shown.length, 1);
		assert.deepEqual(h.notifications, [{ method: 'sourceInspection/release', params: { artifactId: 'one' } }]);
	});
});

for (const language of ['sushi-structured-ir', 'susproj', 'typescript']) {
	test(`generated and unrelated editors cannot issue inspection requests: ${language}`, async () => {
		const h = harness();
		h.source.document.languageId = language;
		await h.commands.get('sushi.showStructuredIR')();
		assert.equal(h.requests.length, 0);
		assert.equal(h.shown.length, 0);
		assert.equal(h.messages.length, 1);
		assert.equal(h.progressCalls.length, 0, 'a fast failure should show its error without progress');
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
	const tabCount = h.tabCount();
	const repeated = await h.definitions[0].provider.provideDefinition(sourceDocument, { line: 2, character: 8 }, { isCancellationRequested: false });
	assert.equal(repeated[0].targetUri.toString(), targetUri);
	assert.equal(h.tabCount(), tabCount);
	assert.equal(h.shown.length, 1, 'the provider must leave editor presentation to VS Code navigation');
});

test('native IR hover providers request semantic metadata and return safe Markdown over the symbol range', async () => {
	const h = harness({ hoverReply: {
		found: true, signature: 'public int32 Helper(int32 input)', details: 'Parameter 0: input\nType: int32',
		range: { start: { line: 2, character: 4 }, end: { line: 2, character: 8 } }
	} });
	assert.deepEqual(h.hovers.map(item => item.selector), [
		{ language: 'sushi-structured-ir', scheme: 'sushi-inspect' },
		{ language: 'sushi-lowered-ir', scheme: 'sushi-inspect' }
	]);
	await h.commands.get('sushi.showStructuredIR')();
	const document = h.shown[0];
	const hover = await h.hovers[0].provider.provideHover(document, { line: 2, character: 6 }, { isCancellationRequested: false });
	assert.equal(h.requests[1].method, 'sourceInspection/hover');
	assert.deepEqual(h.requests[1].params, {
		documentUri: h.reply.documentUri, artifactId: 'one', position: { line: 2, character: 6 }
	});
	assert.deepEqual(hover.range.start, { line: 2, character: 4 });
	assert.deepEqual(hover.contents.value, [
		{ kind: 'code', value: 'public int32 Helper(int32 input)', language: 'sushi' },
		{ kind: 'text', value: 'Parameter 0: input\nType: int32' }
	]);
	assert.equal(hover.contents.isTrusted, false);
	assert.equal(hover.contents.supportHtml, false);
});

test('source definition command opens matching live Sushi content and selects the exact declaration token', async () => {
	const sourceText = 'package Example; namespace Example;\npublic int32 Helper(int32 input) { return input; }';
	const sourceStart = sourceText.indexOf('Helper');
	const h = harness({ sourceText, sourceDefinitionReply: {
		found: true, sourceUri: 'file:///Source.sus', text: sourceText, sourceVersion: 7,
		sourceRange: { start: { line: 1, character: sourceStart }, end: { line: 1, character: sourceStart + 6 } }
	} });
	await h.commands.get('sushi.showStructuredIR')();
	const tabCount = h.tabCount();
	await h.commands.get('sushi.goToSourceDefinition')();
	assert.deepEqual(h.requests[1], {
		method: 'sourceInspection/sourceDefinition', params: {
			documentUri: h.reply.documentUri, artifactId: 'one', position: { line: 0, character: 0 }
		}
	});
	assert.equal(h.shown[1].uri.scheme, 'file');
	assert.equal(h.shown[1].languageId, 'sushi');
	assert.equal(h.tabCount(), tabCount, 'focusing the existing source tab must not make a second source tab');
	assert.equal(h.activeEditor.document, h.sourceDocument);
	assert.deepEqual(h.activeEditor.selection.start, { line: 1, character: sourceStart });
	assert.deepEqual(h.activeEditor.selection.end, { line: 1, character: sourceStart + 6 });
});

test('source navigation reuses an inactive source tab in its original editor group', async () => {
	const sourceText = 'package Example; namespace Example;\npublic int32 Helper() { return 1; }';
	const start = sourceText.indexOf('Helper');
	const h = harness({ sourceTabInactive: true, sourceText, sourceDefinitionReply: {
		found: true, sourceUri: 'file:///Source.sus', text: sourceText, sourceVersion: 7,
		sourceRange: { start: { line: 1, character: start }, end: { line: 1, character: start + 6 } }
	} });
	await h.commands.get('sushi.showStructuredIR')();
	const tabCount = h.tabCount();
	await h.commands.get('sushi.goToSourceDefinition')();
	assert.equal(h.tabCount(), tabCount);
	assert.equal(h.activeEditor.document, h.sourceDocument);
	assert.equal(h.activeEditor.viewColumn, 1);
});

test('source navigation opens a source tab beside IR when no source tab exists', async () => {
	const sourceText = 'package Example; namespace Example;\npublic int32 Helper() { return 1; }';
	const start = sourceText.indexOf('Helper');
	const h = harness({ sourceTab: false, sourceText, sourceDefinitionReply: {
		found: true, sourceUri: 'file:///Source.sus', text: sourceText, sourceVersion: 7,
		sourceRange: { start: { line: 1, character: start }, end: { line: 1, character: start + 6 } }
	} });
	await h.commands.get('sushi.showStructuredIR')();
	const tabCount = h.tabCount();
	await h.commands.get('sushi.goToSourceDefinition')();
	assert.equal(h.tabCount(), tabCount + 1);
	assert.equal(h.activeEditor.document.uri.scheme, 'file');
	assert.equal(h.activeEditor.viewColumn, 2);
});

test('diverged source opens a distinct frozen Sushi historical preview and releases it only on close', async () => {
	const captured = 'package Example; namespace Example;\npublic int32 Helper(int32 input) { return input + 1; }';
	const sourceStart = captured.indexOf('Helper');
	const h = harness({ sourceText: captured.replace('+ 1', '+ 9'), sourceDefinitionReply: {
		found: true, sourceUri: 'file:///Source.sus', text: captured, sourceVersion: 7,
		sourceRange: { start: { line: 1, character: sourceStart }, end: { line: 1, character: sourceStart + 6 } }
	} });
	await h.commands.get('sushi.showLoweredIR')();
	await h.commands.get('sushi.goToSourceDefinition')();
	const preview = h.shown[1];
	assert.equal(preview.uri.scheme, 'sushi-source-snapshot');
	assert.match(preview.uri.toString(), /Source\.sus \(Historical\)/);
	assert.equal(preview.languageId, 'sushi');
	assert.equal(preview.getText(), captured);
	assert.deepEqual(h.activeEditor.selection.start, { line: 1, character: sourceStart });
	assert.equal(h.read(preview.uri.toString()), captured);
	assert.equal(h.notifications.length, 0, 'the source preview does not release the originating inspection artifact');
	h.close(preview);
	await Promise.resolve();
	assert.throws(() => h.read(preview.uri.toString()), /historical source snapshot is no longer available/);
	assert.equal(h.notifications.length, 0);
});

test('repeated historical navigation reuses one snapshot tab while different content remains separate', async () => {
	const firstText = 'package Example; namespace Example;\npublic int32 Helper() { return 1; }';
	const firstStart = firstText.indexOf('Helper');
	const firstResponse = {
		found: true, sourceUri: 'file:///Source.sus', text: firstText, sourceVersion: 7,
		sourceRange: { start: { line: 1, character: firstStart }, end: { line: 1, character: firstStart + 6 } }
	};
	const h = harness({ sourceText: firstText.replace('return 1', 'return 9'), sourceDefinitionReply: firstResponse });
	await h.commands.get('sushi.showStructuredIR')();
	const irDocument = h.shown[0];
	await h.commands.get('sushi.goToSourceDefinition')();
	const firstPreview = h.shown[1];
	const countWithFirstPreview = h.tabCount();
	h.activate(irDocument);
	await h.commands.get('sushi.goToSourceDefinition')();
	assert.equal(h.shown[2], firstPreview);
	assert.equal(h.tabCount(), countWithFirstPreview);

	const secondText = firstText.replace('return 1', 'return 2');
	const secondStart = secondText.indexOf('Helper');
	h.setSourceDefinitionReply({
		found: true, sourceUri: 'file:///Source.sus', text: secondText, sourceVersion: 8,
		sourceRange: { start: { line: 1, character: secondStart }, end: { line: 1, character: secondStart + 6 } }
	});
	h.activate(irDocument);
	await h.commands.get('sushi.goToSourceDefinition')();
	const secondPreview = h.shown[3];
	assert.notEqual(firstPreview.uri.toString(), secondPreview.uri.toString());
	assert.equal(firstPreview.getText(), firstText);
	assert.equal(secondPreview.getText(), secondText);
});

test('unavailable source files use the captured historical snapshot', async () => {
	const captured = 'package Example; namespace Example;\npublic int32 Helper() { return 1; }';
	const start = captured.indexOf('Helper');
	const h = harness({ sourceUnavailable: true, sourceDefinitionReply: {
		found: true, sourceUri: 'file:///Deleted.sus', text: captured, sourceVersion: null,
		sourceRange: { start: { line: 1, character: start }, end: { line: 1, character: start + 6 } }
	} });
	await h.commands.get('sushi.showStructuredIR')();
	await h.commands.get('sushi.goToSourceDefinition')();
	assert.equal(h.shown[1].uri.scheme, 'sushi-source-snapshot');
	assert.equal(h.shown[1].getText(), captured);
});

test('unsupported source symbols and forged responses open no source document', async () => {
	const h = harness({ sourceDefinitionReply: { found: false } });
	await h.commands.get('sushi.showStructuredIR')();
	await h.commands.get('sushi.goToSourceDefinition')();
	assert.equal(h.shown.length, 1);
	assert.match(h.messages[0], /No source declaration/);
});

test('native hover providers stay quiet for missing symbols, cancellation, and server disconnection', async () => {
	const missing = harness({ hoverReply: { found: false } });
	await missing.commands.get('sushi.showStructuredIR')();
	assert.equal(await missing.hovers[0].provider.provideHover(missing.shown[0], { line: 0, character: 0 }, { isCancellationRequested: false }), undefined);
	const cancelled = harness({ hoverReply: { found: true, details: 'should not show', range: { start: { line: 0, character: 0 }, end: { line: 0, character: 1 } } } });
	await cancelled.commands.get('sushi.showLoweredIR')();
	assert.equal(await cancelled.hovers[1].provider.provideHover(cancelled.shown[0], { line: 0, character: 0 }, { isCancellationRequested: true }), undefined);
	assert.equal(cancelled.requests.length, 1, 'a canceled hover must not make a server request');
	const disconnectedOptions = {};
	const disconnected = harness(disconnectedOptions);
	await disconnected.commands.get('sushi.showStructuredIR')();
	disconnectedOptions.disconnected = true;
	assert.equal(await disconnected.hovers[0].provider.provideHover(disconnected.shown[0], { line: 0, character: 0 }, { isCancellationRequested: false }), undefined);
});

for (const [command, providerIndex, stage, suffix] of [
	['sushi.showStructuredIR', 0, 'structuredIR', 'structuredIR'],
	['sushi.showLoweredIR', 1, 'loweredIR', 'loweredIR']
]) {
	test(`the native ${stage} definition provider returns no target for an already-defined symbol`, async () => {
		const h = harness({ definitionReply: { found: false } });
		h.reply.stage = stage;
		h.reply.documentUri = `sushi-inspect://session/one/Value.${suffix}`;
		await h.commands.get(command)();
		const sourceDocument = h.shown[0];
		const location = await h.definitions[providerIndex].provider.provideDefinition(sourceDocument, { line: 0, character: 10 }, { isCancellationRequested: false });
		assert.equal(location, undefined);
		assert.equal(h.requests[1].method, 'sourceInspection/definition');
		assert.equal(h.shown.length, 1, 'the provider should keep the original document and create no self-target');
	});
}

for (const [command, stage, failureText] of [
	['sushi.showStructuredIR', 'Structured IR', 'The selected activity entry cannot be inspected'],
	['sushi.showLoweredIR', 'Lowered IR', 'The selected activity entry cannot be inspected']
]) {
	test(`${stage} fast failure shows its error without a progress notification`, async () => {
		let dismissError;
		const errorPromise = new Promise(resolve => { dismissError = resolve; });
		const h = harness({ failure: true, failureText, errorPromise });
		let commandSettled = false;
		const running = h.commands.get(command)().finally(() => { commandSettled = true; });
		await h.errorShown;
		assert.deepEqual(h.errorProgressStates, [true]);
		assert.equal(h.progressCalls.length, 0);
		assert.equal(h.shown.length, 0);
		assert.equal(commandSettled, false, 'the command may wait for the message UI, but progress has already settled');
		dismissError();
		await running;
		assert.match(h.messages[0], /selected activity entry/);
	});
}

test('a fast unexpected request exception shows its error without progress', async () => {
	let dismissError;
	const errorPromise = new Promise(resolve => { dismissError = resolve; });
	const h = harness({ throwRequest: true, errorPromise });
	const running = h.commands.get('sushi.showLoweredIR')();
	await h.errorShown;
	assert.deepEqual(h.errorProgressStates, [true]);
	assert.equal(h.progressCalls.length, 0);
	assert.equal(h.shown.length, 0);
	dismissError();
	await running;
	assert.equal(h.messages[0], 'Sushi inspection failed. Check the language-server connection and the Sushi Inspection output.');
});

test('slow semantic failure dismisses progress before presenting its diagnostic', async () => {
	await withManualTimers(async clock => {
		const operation = deferred();
		const h = harness({ requestHandler: () => operation.promise });
		const running = h.commands.get('sushi.showStructuredIR')();
		clock.advanceBy(500);
		assert.equal(h.progressCalls.length, 1);
		operation.resolve({ succeeded: false, error: 'Inspection failed', projects: [], diagnostics: [
			{ code: 'InvalidSignature', message: 'The return type is invalid', line: 2, character: 4 }
		] });
		await h.errorShown;
		assert.equal(h.progressCalls[0].settled, true);
		assert.deepEqual(h.errorProgressStates, [true]);
		assert.match(h.messages[0], /InvalidSignature: The return type is invalid \(3:5\)/);
		assert.equal(h.shown.length, 0);
		await running;
	});
});

test('failure after registering the result releases its artifact and removes virtual content', async () => {
	const h = harness({ openFailure: true });
	await h.commands.get('sushi.showStructuredIR')();
	assert.equal(h.shown.length, 0);
	assert.deepEqual(h.notifications[0], { method: 'sourceInspection/release', params: { artifactId: 'one' } });
	assert.throws(h.read, /no longer available/);
	assert.deepEqual(h.errorProgressStates, [true]);
	assert.equal(h.progressCalls.length, 0, 'document presentation occurs after the completed request progress scope');
});

test('cancellation rejection after visible progress is quiet and dismisses progress before returning', async () => {
	await withManualTimers(async clock => {
		const operation = deferred();
		const h = harness({ requestHandler: () => operation.promise });
		const running = h.commands.get('sushi.showLoweredIR')();
		clock.advanceBy(500);
		h.cancelProgress(0);
		operation.reject(new Error('Request cancelled'));
		await running;
		assert.equal(h.messages.length, 0);
		assert.equal(h.shown.length, 0);
		assert.equal(h.progressCalls[0].settled, true);
		assert.equal(h.errorProgressStates.length, 0);
	});
});
