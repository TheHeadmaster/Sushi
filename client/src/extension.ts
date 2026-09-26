import * as net from 'net';
import * as path from 'path';
import {
	ExtensionContext,
	ExtensionMode
} from 'vscode';

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

	const clientOptions: LanguageClientOptions = {
		documentSelector: [
			{ scheme: 'file', language: 'sushi' },
			{ scheme: 'file', language: 'susproj' },
			{ scheme: 'file', language: 'susln' }
		],
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
	const debugPort = getDebugLspPort(context);

	if (debugPort !== undefined) {
		return () => connectToServer('127.0.0.1', debugPort);
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

const developmentLspPort = 5057;

function getDebugLspPort(context: ExtensionContext
): number | undefined {
	const configuredPort = process.env.SUSHI_LSP_DEBUG_PORT;

	if (configuredPort && configuredPort.trim().length > 0) {
		const port = Number(configuredPort);

		if (!Number.isInteger(port) || port <= 0 || port > 65535) {
			throw new Error(`Invalid SUSHI_LSP_DEBUG_PORT value: "${configuredPort}"`);
		}

		return port;
	}

	if (context.extensionMode === ExtensionMode.Development) {
		return developmentLspPort;
	}

	return undefined;
}

function connectToServer(host: string, port: number): Promise<StreamInfo> {
	return new Promise<StreamInfo>((resolve, reject) => {
		const socket = new net.Socket();

		const onError = (error: Error) => {
			socket.destroy();

			reject(new Error(`Could not connect to Sushi LSP at ${host}:${port}: ${error.message}`));
		};

		socket.once('error', onError);

		socket.once('connect', () => {
			socket.removeListener('error', onError);

			resolve({
				reader: socket,
				writer: socket
			});
		});

		socket.connect(port, host);
	});
}