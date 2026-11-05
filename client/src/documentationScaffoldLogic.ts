export interface DocumentationScaffoldResponse {
	readonly eligible: boolean;
	readonly documentVersion?: number;
	readonly parameterNames: readonly string[];
	readonly hasReturnDescription: boolean;
}

export interface TripleSlashTyping {
	readonly indentation: string;
	readonly slashColumn: number;
}

/** Identifies the single slash typed after an indentation-only pair of slashes. */
export function identifyThirdSlashTyping(
	typedText: string,
	beforeLine: string,
	beforeCharacter: number,
	afterLine: string,
	afterCharacter: number,
	selectionWasEmpty: boolean
): TripleSlashTyping | undefined {
	if (typedText !== '/' || !selectionWasEmpty || beforeCharacter !== beforeLine.length
		|| afterCharacter !== afterLine.length || afterCharacter !== beforeCharacter + 1) {
		return undefined;
	}

	const before = /^([\t ]*)\/\/$/.exec(beforeLine);
	const after = /^([\t ]*)\/\/\/$/.exec(afterLine);
	if (!before || !after || before[1] !== after[1]) {
		return undefined;
	}

	return { indentation: after[1], slashColumn: after[1].length };
}

/** Builds editor snippet syntax; LSP responses contain metadata, never tab-stop edits. */
export function createDocumentationScaffoldSnippet(
	parameterNames: readonly string[],
	hasReturnDescription: boolean,
	lineEnding: string
): string {
	const lines = [`/// ${placeholder(1)}`];
	let tabStop = 2;

	if (parameterNames.length > 0 || hasReturnDescription) {
		lines.push('///');
		for (const name of parameterNames) {
			lines.push(`/// @param ${escapeSnippetText(name)} ${placeholder(tabStop++)}`);
		}
		if (hasReturnDescription) {
			lines.push(`/// @return ${placeholder(tabStop)}`);
		}
	}

	return lines.join(lineEnding);
}

export class DocumentationScaffoldRequestGate {
	private readonly latestRequests = new Map<string, string>();

	shouldHandle(uri: string, version: number, line: number): boolean {
		const key = `${version}:${line}`;
		if (this.latestRequests.get(uri) === key) {
			return false;
		}
		this.latestRequests.set(uri, key);
		return true;
	}
}

export function isScaffoldResponseCurrent(expectedUri: string, expectedVersion: number, actualUri: string, actualVersion: number): boolean {
	return expectedUri === actualUri && expectedVersion === actualVersion;
}

function placeholder(index: number): string {
	return '$' + '{' + index + ':}';
}

function escapeSnippetText(value: string): string {
	return value.replace(/[\\$}]/g, '\\$&');
}
