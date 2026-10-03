import type { ConfigAddress, ConfigEntry, SiteConfig } from './web-editor/core/types'

// Reading the owner's site config (`config.fields.<key>`, `config.groups.<key>`) in the public site, templates and
// overrides. Pure functions: server-rendered, no window/document.

/** The owner's firm name (core field `firmName`), trimmed; '' when not filled. */
export function firmName(config: SiteConfig | null): string {
	return config?.fields.firmName?.trim() ?? ''
}

/** First entry with `key` in group `group` (keys are unique per group), or undefined. */
export function configEntry(config: SiteConfig | null, group: string, key: string): ConfigEntry | undefined {
	return config?.groups[group]?.find((entry) => entry.key === key)
}

/** Entries of a group that have something to show (empty values / empty addresses left out). */
export function filledEntries(config: SiteConfig | null, group: string): ConfigEntry[] {
	return config?.groups[group]?.filter(hasValue) ?? []
}

export function hasValue(entry: ConfigEntry): boolean {
	return entry.type === 'address' ? addressLines(entry.address).length > 0 : !!entry.value
}

/** A blank address (new address entries, type switched to address). */
export const emptyAddress = (): ConfigAddress => ({ street: '', postalCode: '', city: '', country: '' })

/** Address as display lines: street / postal code + city / country (empty parts skipped). */
export function addressLines(address: ConfigAddress | null | undefined): string[] {
	if (!address) return []
	const town = [address.postalCode, address.city].filter(Boolean).join(' ')
	return [address.street, town, address.country].filter(Boolean)
}

/** Link target for an entry by type: mailto:, tel: (digits and + only), the URL itself; null for text/address. */
export function entryHref(entry: ConfigEntry): string | null {
	switch (entry.type) {
		case 'email':
			return `mailto:${entry.value}`
		case 'phone':
			return `tel:${entry.value.replace(/[^\d+]/g, '')}`
		case 'link':
			return entry.value
		default:
			return null
	}
}

/** Shown text of a link entry: without scheme/trailing slash ("instagram.com/acme"); other types as entered. */
export function entryText(entry: ConfigEntry): string {
	return entry.type === 'link' ? entry.value.replace(/^https?:\/\//, '').replace(/\/$/, '') : entry.value
}
