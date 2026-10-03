export const toneOptions = [
	{ value: 'info', label: 'Info' },
	{ value: 'warning', label: 'Warning' },
]

/** Same classes in Edit and View (edit/view parity). */
export const toneClass = (tone: unknown) =>
	tone === 'warning' ? 'border-amber-500 bg-amber-50' : 'border-brand bg-brand/10'
