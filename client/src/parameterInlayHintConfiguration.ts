export type ParameterInlayHintMode = 'off' | 'selective' | 'all';

export function normalizeParameterInlayHintMode(value: unknown): ParameterInlayHintMode {
	if (value === 'off' || value === 'selective' || value === 'all') {
		return value;
	}

	return 'selective';
}
