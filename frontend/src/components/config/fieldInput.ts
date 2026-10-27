import type { ConfigFieldType } from '../../lib/web-editor'

/** Message keys of the type names. */
export const TYPE_LABELS: Record<ConfigFieldType, string> = {
	text: 'admin.config.types.text',
	email: 'admin.config.types.email',
	phone: 'admin.config.types.phone',
	link: 'admin.config.types.link',
	address: 'admin.config.types.address',
}

/** Message keys of the value placeholders (the email one is literal: `@` is special in messages). */
export const VALUE_PLACEHOLDERS: Record<Exclude<ConfigFieldType, 'address'>, string> = {
	text: 'admin.config.placeholders.text',
	email: 'name@example.com',
	phone: 'admin.config.placeholders.phone',
	link: 'https://…',
}

/** A value placeholder, translated where it is a message key. */
export const valuePlaceholder = (type: Exclude<ConfigFieldType, 'address'>, t: (key: string) => string) =>
	VALUE_PLACEHOLDERS[type].startsWith('admin.') ? t(VALUE_PLACEHOLDERS[type]) : VALUE_PLACEHOLDERS[type]

// mobile keyboard per field type
export const INPUT_TYPES = { text: 'text', email: 'email', phone: 'tel', link: 'url' } as const satisfies Record<
	Exclude<ConfigFieldType, 'address'>,
	string
>
