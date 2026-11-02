import test from 'node:test';
import assert from 'node:assert/strict';
import { InspectionDocuments } from '../out/inspectionDocuments.js';

function artifact(id, stage = 'structuredIR', text = 'public function void F()\n{\n}\n') {
	return { succeeded: true, artifactId: id, documentUri: `sushi-inspect://session/${id}/F.${stage}`,
		stage, text, functionLabel: 'F', sourceUri: 'file:///Source.sus', sourceVersion: 1 };
}

test('virtual content preserves exact formatter bytes and never replaces an old snapshot', () => {
	const documents = new InspectionDocuments();
	const first = artifact('one');
	documents.add(first);
	first.text = 'changed externally';
	documents.add(artifact('two', 'loweredIR', 'lowered\n'));
	assert.equal(documents.read(first.documentUri), 'public function void F()\n{\n}\n');
	assert.equal(documents.read(artifact('two', 'loweredIR').documentUri), 'lowered\n');
	assert.throws(() => documents.add(artifact('one')), /cannot replace/);
});

test('close removes only the matching artifact and shutdown clears remaining documents', () => {
	const documents = new InspectionDocuments();
	documents.add(artifact('one'));
	documents.add(artifact('two'));
	assert.equal(documents.remove(artifact('one').documentUri), 'one');
	assert.equal(documents.remove(artifact('one').documentUri), undefined);
	assert.throws(() => documents.read(artifact('one').documentUri), /no longer available/);
	assert.ok(documents.read(artifact('two').documentUri));
	documents.clear();
	assert.throws(() => documents.read(artifact('two').documentUri), /no longer available/);
});

test('invalid, empty, or writable-file results cannot become virtual documents', () => {
	const documents = new InspectionDocuments();
	for (const update of [{ succeeded: false }, { stage: 'llvm' }, { text: '' }, { documentUri: 'file:///generated.sus' }]) {
		assert.throws(() => documents.add({ ...artifact('bad'), ...update }), /invalid inspection artifact/);
	}
});
