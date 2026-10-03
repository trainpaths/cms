import type { ConfigFieldType } from '../../lib/web-editor'

export const TYPE_LABELS: Record<ConfigFieldType, string> = {
	text: 'Text',
	email: 'Email',
	phone: 'Phone',
	link: 'Link',
	address: 'Address',
}

export const VALUE_PLACEHOLDERS: Record<Exclude<ConfigFieldType, 'address'>, string> = {
	text: 'Value',
	email: 'name@example.com',
	phone: '+49 123 456789',
	link: 'https://…',
}

// mobile keyboard per field type
export const INPUT_TYPES = { text: 'text', email: 'email', phone: 'tel', link: 'url' } as const satisfies Record<
	Exclude<ConfigFieldType, 'address'>,
	string
>
