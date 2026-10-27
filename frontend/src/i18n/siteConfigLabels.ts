import { useI18n } from 'vue-i18n'
import { SOURCE_LANG } from '.'

/**
 * A name that comes from configuration in English (site config fields from the API / the instance config, block
 * titles from `block.json`), translated by the message `key` (package: the built-in ones; instances add theirs in
 * `src/locales/`) unless the config renamed it (the English message no longer matches): the developer's wording wins.
 */
export function useConfiguredLabel() {
	const { t, te } = useI18n()
	return (key: string, fallback: string) =>
		te(key) && (!te(key, SOURCE_LANG) || t(key, {}, { locale: SOURCE_LANG }) === fallback) ? t(key) : fallback
}

/** Site config fields, groups and preset entries: `siteConfig.fields.<key>`, `.groups.<key>`, `.entries.<group>.<key>`. */
export function useConfigLabels() {
	const label = useConfiguredLabel()
	return {
		field: (key: string, fallback: string) => label(`siteConfig.fields.${key}`, fallback),
		group: (key: string, fallback: string) => label(`siteConfig.groups.${key}`, fallback),
		entry: (group: string, key: string, fallback: string) => label(`siteConfig.entries.${group}.${key}`, fallback),
	}
}

/** Block type title + description in the editor: `blocks.<name>.title` / `.description`. */
export function useBlockLabels() {
	const label = useConfiguredLabel()
	return {
		title: (type: { name: string; title: string }) => label(`blocks.${type.name}.title`, type.title),
		description: (type: { name: string; description?: string }) =>
			type.description ? label(`blocks.${type.name}.description`, type.description) : '',
	}
}
