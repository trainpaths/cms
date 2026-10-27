export type Align = 'left' | 'center' | 'right'

/** label = message key */
export const alignOptions: { value: Align; label: string }[] = [
	{ value: 'left', label: 'editor.align.left' },
	{ value: 'center', label: 'editor.align.center' },
	{ value: 'right', label: 'editor.align.right' },
]
