import assert from 'node:assert/strict';
import test from 'node:test';
import {
	createDocumentationScaffoldReplacementRange,
	createDocumentationScaffoldSnippet,
	DocumentationScaffoldRequestGate,
	identifyThirdSlashTyping,
	isScaffoldResponseCurrent
} from '../out/documentationScaffoldLogic.js';

test('recognizes only a typed third slash after indentation and preserves the insertion column', () => {
	assert.deepEqual(identifyThirdSlashTyping('/', '\t  //', 5, '\t  ///', 6, true), {
		indentation: '\t  ', slashColumn: 3
	});
	assert.equal(identifyThirdSlashTyping('///', '', 0, '///', 3, true), undefined);
	assert.deepEqual(identifyThirdSlashTyping('/', ' //', 3, ' ///', 4, true), {
		indentation: ' ', slashColumn: 1
	});
	assert.equal(identifyThirdSlashTyping('/', 'x//', 3, 'x///', 4, true), undefined);
	assert.equal(identifyThirdSlashTyping('/', ' //', 2, ' ///', 4, true), undefined);
	assert.equal(identifyThirdSlashTyping('/', '  //', 4, '  ///', 5, false), undefined);
});

test('places description, parameter, and return placeholders in declaration order', () => {
	const snippet = createDocumentationScaffoldSnippet(['left', 'right'], true, '', '\n');
	assert.equal(snippet, '/// ${1:}\n///\n/// @param left ${2:}\n/// @param right ${3:}\n/// @return ${4:}\n$0');
});

test('omits return descriptions for void callables and emits no extra line for empty descriptions', () => {
	assert.equal(createDocumentationScaffoldSnippet([], false, '', '\n'), '/// ${1:}\n$0');
	assert.equal(createDocumentationScaffoldSnippet(['only'], false, '', '\r\n'), '/// ${1:}\r\n///\r\n/// @param only ${2:}\r\n$0');
});

test('keeps the requested line ending and escapes static snippet text', () => {
	assert.equal(createDocumentationScaffoldSnippet(['a$}\\b'], true, '', '\r\n'),
		'/// ${1:}\r\n///\r\n/// @param a\\$\\}\\\\b ${2:}\r\n/// @return ${3:}\r\n$0');
});

test('places the final snippet stop on the declaration line without detaching documentation', () => {
	const indentation = '  ';
	const snippet = createDocumentationScaffoldSnippet(['value'], true, indentation, '\r\n');
	assert.equal(snippet, '/// ${1:}\r\n  ///\r\n  /// @param value ${2:}\r\n  /// @return ${3:}\r\n$0');
	assert.deepEqual(createDocumentationScaffoldReplacementRange(3, 2), {
		start: { line: 3, character: 2 }, end: { line: 4, character: 0 }
	});

	const source = '  ///\r\n  public void Run() {}';
	const replacedLineEnding = source.indexOf('\r\n') + 2;
	const expandedText = snippet.replace(/\$\{\d+:\}/g, '').replace(/\$0$/, '');
	const result = source.slice(0, 2) + expandedText + source.slice(replacedLineEnding);
	assert.equal(result, '  /// \r\n  ///\r\n  /// @param value \r\n  /// @return \r\n  public void Run() {}');
	assert.equal(snippet.endsWith('\r\n$0'), true);
});

test('suppresses duplicate requests for one document version and line while allowing later versions', () => {
	const gate = new DocumentationScaffoldRequestGate();
	assert.equal(gate.shouldHandle('file:///main.sus', 8, 4), true);
	assert.equal(gate.shouldHandle('file:///main.sus', 8, 4), false);
	assert.equal(gate.shouldHandle('file:///main.sus', 9, 4), true);
	assert.equal(gate.shouldHandle('file:///other.sus', 8, 4), true);
});

test('rejects stale responses after a document or document version changes', () => {
	assert.equal(isScaffoldResponseCurrent('file:///main.sus', 8, 'file:///main.sus', 8), true);
	assert.equal(isScaffoldResponseCurrent('file:///main.sus', 8, 'file:///main.sus', 9), false);
	assert.equal(isScaffoldResponseCurrent('file:///main.sus', 8, 'file:///other.sus', 8), false);
});
