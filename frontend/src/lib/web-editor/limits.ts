import type { ConfigFieldType } from './core/types'

// Mirror the API's validation so bad input fails before the request.

export const MEDIA_ACCEPT = 'image/jpeg,image/png,image/gif,image/webp,image/avif'
export const MEDIA_MAX_BYTES = 10 * 1024 * 1024
export const MEDIA_ALT_MAX = 100
export const MEDIA_FILENAME_MAX = 255

/** `ConfigLimits` / `ConfigFieldType` in the API (site config entries). */
export const CONFIG_MAX_ENTRIES = 50
export const CONFIG_KEY_MAX = 50
export const CONFIG_VALUE_MAX = 1000
export const CONFIG_ADDRESS_PART_MAX = 200
export const CONFIG_FIELD_TYPES: ConfigFieldType[] = ['text', 'email', 'phone', 'link', 'address']

/** `PageService.MaxMetaTitleLength` / `MaxMetaDescriptionLength` in the API. */
export const META_TITLE_MAX = 70
export const META_DESCRIPTION_MAX = 200

/** `TagLimits` + `Tag.Normalize` in the API. */
export const TAG_MAX_LENGTH = 32
export const TAGS_PER_PAGE = 20
/** Tags are single words: separators (space, comma) split them before this runs. */
export const normalizeTag = (raw: string) => raw.trim().toLowerCase()

/** `MenuLimits` + `MenuService.NormalizeHandle` in the API. */
export const MENU_HANDLE_MAX = 50
export const normalizeHandle = (raw: string) =>
	raw
		.normalize('NFD')
		.replace(/\p{M}/gu, '')
		.toLowerCase()
		.replace(/[^a-z0-9]+/g, '-')
		.replace(/^-+|-+$/g, '')
export const MENU_LABEL_MAX = 100
export const MENU_URL_MAX = 1000
export const MENU_MAX_DEPTH = 10
export const MENU_MAX_ITEMS = 200
