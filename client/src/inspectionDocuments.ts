export type InspectionStage = 'structuredIR' | 'loweredIR';

export interface InspectionSuccess {
	readonly succeeded: true;
	readonly artifactId: string;
	readonly documentUri: string;
	readonly stage: InspectionStage;
	readonly text: string;
	readonly functionLabel: string;
	readonly sourceUri: string;
	readonly sourceVersion: number | null;
}

export interface InspectionFailure {
	readonly succeeded: false;
	readonly error: string;
	readonly projects: string[];
	readonly diagnostics: { code: string; message: string; line: number; character: number }[];
}

export type InspectionResponse = InspectionSuccess | InspectionFailure;

/** Frozen content owned by open virtual documents. Closing a document releases its server artifact separately. */
export class InspectionDocuments {
	private readonly documents = new Map<string, Readonly<InspectionSuccess>>();

	add(result: InspectionSuccess): void {
		if (result.succeeded !== true || !result.artifactId || typeof result.text !== 'string' || !result.text
			|| (result.stage !== 'structuredIR' && result.stage !== 'loweredIR')
			|| new URL(result.documentUri).protocol !== 'sushi-inspect:') {
			throw new Error('The language server returned an invalid inspection artifact.');
		}
		if (this.documents.has(result.documentUri)) {
			throw new Error('An inspection document cannot replace an existing snapshot.');
		}
		this.documents.set(result.documentUri, Object.freeze({ ...result }));
	}

	read(uri: string): string {
		const result = this.documents.get(uri);
		if (!result) {
			throw new Error('This inspection document is no longer available. Request inspection again.');
		}
		return result.text;
	}

	remove(uri: string): string | undefined {
		const result = this.documents.get(uri);
		this.documents.delete(uri);
		return result?.artifactId;
	}

	clear(): void {
		this.documents.clear();
	}
}
