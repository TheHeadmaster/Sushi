import { randomUUID } from 'crypto';
import * as vscode from 'vscode';

export interface SourceSnapshotContent {
	readonly sourceUri: string;
	readonly sourceVersion: number | null;
	readonly text: string;
}

interface RetainedSourceSnapshot extends SourceSnapshotContent {
	readonly uri: string;
}

/** Owns frozen historical source text only while its virtual document is retained by the extension. */
export class SourceSnapshotDocuments {
	private readonly documents = new Map<string, Readonly<RetainedSourceSnapshot>>();
	private readonly snapshotUris = new Map<string, string>();

	constructor(private readonly capacity = 32) {
		if (!Number.isInteger(capacity) || capacity < 1) {
			throw new RangeError('Historical source capacity must be a positive integer.');
		}
	}

	add(snapshot: SourceSnapshotContent): string {
		if (!snapshot.sourceUri || typeof snapshot.text !== 'string' || new URL(snapshot.sourceUri).protocol !== 'file:') {
			throw new Error('The language server returned an invalid historical source snapshot.');
		}
		const key = JSON.stringify([snapshot.sourceUri, snapshot.sourceVersion, snapshot.text]);
		const existingUri = this.snapshotUris.get(key);
		if (existingUri && this.documents.has(existingUri)) {
			return existingUri;
		}
		if (this.documents.size >= this.capacity) {
			throw new Error('Too many historical source previews are open. Close an older preview and retry.');
		}

		const fileName = decodeURIComponent(new URL(snapshot.sourceUri).pathname.split('/').pop() || 'Source.sus');
		const uri = vscode.Uri.from({
			scheme: 'sushi-source-snapshot',
			path: `/${randomUUID()}/${fileName} (Historical)`
		}).toString();
		this.documents.set(uri, Object.freeze({ ...snapshot, uri }));
		this.snapshotUris.set(key, uri);
		return uri;
	}

	read(uri: string): string {
		const snapshot = this.documents.get(uri);
		if (!snapshot) {
			throw new Error('This historical source snapshot is no longer available. Navigate from the inspection again.');
		}
		return snapshot.text;
	}

	remove(uri: string): void {
		const snapshot = this.documents.get(uri);
		if (!snapshot) {
			return;
		}
		this.documents.delete(uri);
		this.snapshotUris.delete(JSON.stringify([snapshot.sourceUri, snapshot.sourceVersion, snapshot.text]));
	}

	clear(): void {
		this.documents.clear();
		this.snapshotUris.clear();
	}
}
