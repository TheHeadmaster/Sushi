import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { normalizeParameterInlayHintMode } from '../out/parameterInlayHintConfiguration.js';

const packageManifest = JSON.parse(readFileSync(new URL('../../package.json', import.meta.url), 'utf8'));
const hintModeSetting = packageManifest.contributes.configuration.properties['sushi.parameterInlayHints.mode'];

test('parameter inlay hint modes are registered with selective as the default', () => {
	assert.deepEqual(hintModeSetting.enum, ['off', 'selective', 'all']);
	assert.equal(hintModeSetting.default, 'selective');
	assert.match(hintModeSetting.description, /parameter-name inlay hints/);
});

test('configuration normalization accepts each presentation mode and falls back to selective', () => {
	assert.equal(normalizeParameterInlayHintMode('off'), 'off');
	assert.equal(normalizeParameterInlayHintMode('selective'), 'selective');
	assert.equal(normalizeParameterInlayHintMode('all'), 'all');
	assert.equal(normalizeParameterInlayHintMode(undefined), 'selective');
	assert.equal(normalizeParameterInlayHintMode('invalid'), 'selective');
});
