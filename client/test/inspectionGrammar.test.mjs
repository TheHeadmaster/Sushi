import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { URL } from 'node:url';
import { load } from 'js-yaml';

for (const stage of ['structured', 'lowered']) {
	test(`${stage} grammar JSON is reproducible from YAML and all lexical patterns compile`, () => {
		const json = JSON.parse(readFileSync(new URL(`../../syntaxes/sushi-${stage}-ir.tmLanguage.json`, import.meta.url), 'utf8'));
		const yaml = load(readFileSync(new URL(`../../syntaxes/sushi-${stage}-ir.tmLanguage.yml`, import.meta.url), 'utf8'));
		assert.deepEqual(json, yaml);
		assert.equal(json.scopeName, `source.sushi.${stage}-ir`);
		for (const pattern of json.patterns) {
			assert.doesNotThrow(() => new RegExp(pattern.match, 'g'));
		}
		const keywords = json.patterns.find(pattern => pattern.captures?.['1']?.name === 'keyword.control.sushi-ir');
		assert.equal(new RegExp(keywords.match).exec('        returnValue;'), null);
		assert.equal(new RegExp(keywords.match).exec('        return 2;')[1], 'return');
	});
}

test('lowered grammar distinguishes both shift labels and SSA storage from plain numbers', () => {
	const grammar = JSON.parse(readFileSync(new URL('../../syntaxes/sushi-lowered-ir.tmLanguage.json', import.meta.url), 'utf8'));
	const instruction = grammar.patterns.find(pattern => pattern.name === 'keyword.other.instruction.sushi-ir');
	for (const label of ['checked_shift', 'count_checked_shift', 'phi', 'load', 'checked_negate', 'eager']) {
		assert.equal(new RegExp(instruction.match).exec(`        int32 %temp0 = ${label} 1, 2;`)[0], label);
	}
	assert.equal(new RegExp(instruction.match).exec('        int32 %temp0 = count_checked_shifted;'), null);
	const temporary = grammar.patterns.find(pattern => pattern.name === 'variable.other.temporary.sushi-ir');
	assert.equal(new RegExp(temporary.match).exec('        bool %temp42 = phi')[0], '%temp42');
});

test('contributions restrict inspection actions to ordinary Sushi source editors and register both generated languages', () => {
	const manifest = JSON.parse(readFileSync(new URL('../../package.json', import.meta.url), 'utf8'));
	const when = 'editorLangId == sushi && resourceScheme == file && resourceExtname == .sus';
	for (const [stage, command] of [['structured', 'sushi.showStructuredIR'], ['lowered', 'sushi.showLoweredIR']]) {
		assert.ok(manifest.contributes.languages.some(language => language.id === `sushi-${stage}-ir`));
		assert.ok(manifest.contributes.grammars.some(grammar => grammar.language === `sushi-${stage}-ir` && grammar.scopeName === `source.sushi.${stage}-ir`));
		assert.equal(manifest.contributes.commands.find(item => item.command === command).enablement, when);
		assert.equal(manifest.contributes.menus['editor/context'].find(item => item.command === command).when, when);
		assert.equal(manifest.contributes.menus.commandPalette.find(item => item.command === command).when, when);
	}
});
