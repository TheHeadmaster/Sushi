import * as net from 'net';
import * as path from 'path';
import { ExtensionContext, workspace } from 'vscode';

import {
	Executable,
	LanguageClient,
	LanguageClientOptions,
	ServerOptions,
	StreamInfo,
	TransportKind
} from 'vscode-languageclient/node';

let client: LanguageClient | undefined;

export async function activate(context: ExtensionContext): Promise<void> {
	const serverOptions = createServerOptions(context);

	const watcher = workspace.createFileSystemWatcher('**/*.{sus,susproj,susln');

	const clientOptions: LanguageClientOptions = {
		documentSelector: [
			{ scheme: 'file', language: 'sushi' },
			{ scheme: 'file', language: 'susproj' },
			{ scheme: 'file', language: 'susln' }
		],

		synchronize: {
			fileEvents: watcher
		}
	};

	client = new LanguageClient(
		'sushiLanguageServer',
		'Sushi Language Server',
		serverOptions,
		clientOptions
	);

	await client.start();
}

export function deactivate(): Thenable<void> | undefined {
	if (!client) {
		return undefined;
	}
	return client.stop();
}

function createServerOptions(context: ExtensionContext): ServerOptions {
	const debugPortText = process.env.SUSHI_LSP_DEBUG_PORT;

	if (debugPortText && debugPortText.trim().length > 0) {
		const debugPort = Number(debugPortText);

		if (!Number.isInteger(debugPort) || debugPort <= 0 || debugPort > 65535) {
			throw new Error(`Invalid SUSHI_LSP_DEBUG_PORT value: "${debugPortText}"`);
		}

		return () => connectWithRetry('127.0.0.1', debugPort, 40, 250);
	}

	const command = getBundledServerCommand(context);

	const executable: Executable = {
		command,
		args: ['-lsp', '-stdio'],
		transport: TransportKind.stdio
	};

	return {
		run: executable,
		debug: executable
	};
}

function getBundledServerCommand(context: ExtensionContext): string {
	// Placeholder for future platforms
	if (process.platform === 'win32') {
		return context.asAbsolutePath(path.join('server', 'Sushi.exe'));
	}

	return context.asAbsolutePath(path.join('server', "Sushi"));
}

function connectWithRetry(host: string, port: number, maxAttempts: number, delayMilliseconds: number): Promise<StreamInfo> {
	return new Promise<StreamInfo>((resolve, reject) => {
		let attempt = 0;

		const tryConnect = () => {
			attempt++;

			const socket = net.connect(port, host);

			const onError = (error: Error) => {
				socket.removeAllListeners();

				if (attempt >= maxAttempts) {
					reject(new Error(`Could not connect to Sushi LSP at ${host}:${port} after ${attempt} attempts. Last error: ${error.message}`));
					return;
				}

				setTimeout(tryConnect, delayMilliseconds);
			};

			socket.once('connect', () => {
				socket.removeListener('error', onError);

				resolve({ reader: socket, writer: socket });
			});

			socket.once('error', onError);
		};

		tryConnect();
	});
}