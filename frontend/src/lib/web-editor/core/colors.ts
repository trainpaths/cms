/**
 * Block colours (`backgroundColor` / `textColor` attributes). The palette is the instance's `cms.config.json`
 * `theme.colors` (key → CSS colour), set at build time by `cms()` (server/theme.js), which also defines
 * `--color-<key>` on `.site-theme`. Blocks store the key, so a theme change recolours existing content.
 */

declare const __CMS_THEME_COLORS__: Record<string, string> | undefined

// undefined outside cms() (Vitest)
export const themeColors: Record<string, string> =
	typeof __CMS_THEME_COLORS__ === 'undefined' ? {} : __CMS_THEME_COLORS__

const KEY = /^[a-z0-9]+(?:-[a-z0-9]+)*$/
// before the palette, colours were free hex values
const LEGACY_HEX = /^#[0-9a-fA-F]{3,8}$/

/** CSS value of a stored block colour: key → `var(--color-key)`, legacy hex as is, anything else none. */
export function colorValue(value: unknown): string | undefined {
	if (typeof value !== 'string' || !value) return undefined
	if (KEY.test(value)) return `var(--color-${value})`
	if (LEGACY_HEX.test(value)) return value
	return undefined
}

/** Inline style of a block wrapper (the one allowed inline style). */
export function blockColorStyle(attributes: Record<string, unknown>): Record<string, string> {
	const styles: Record<string, string> = {}
	const bg = colorValue(attributes.backgroundColor)
	const text = colorValue(attributes.textColor)
	if (bg) styles.backgroundColor = bg
	if (text) styles.color = text
	return styles
}
