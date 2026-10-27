import type { Component } from 'vue'

export interface AttributeDefinition {
	type: 'string' | 'number' | 'boolean'
	default: string | number | boolean
	/**
	 * Text that differs per language (a value per language in `BlockInstance.translations`, the default language's in
	 * `attributes`). Everything else (media, layout, colours) is shared by all languages.
	 */
	translatable?: boolean
}

export type TranslationValue = string | number | boolean

export interface BlockSupports {
	width?: boolean
	/** true = the whole theme palette; a list = only these palette keys, in this order */
	backgroundColor?: boolean | string[]
	textColor?: boolean | string[]
}

export type BlockCategory = 'text' | 'media' | 'containers' | 'embeds'

export interface BlockType {
	name: string
	title: string
	category: BlockCategory
	icon: Component
	description?: string
	supports?: BlockSupports
	attributes: Record<string, AttributeDefinition>
	allowedBlocks?: string[]
	/** Block may only be placed inside these block types (never top level), e.g. list-item → list. */
	parent?: string[]
	/** Public render component lives in `blockViews.ts`, not here (keeps editor code out of the public bundle). */
	edit: Component
	/** Optional block-specific sidebar panel; receives `block` prop. */
	settings?: Component
}

export interface BlockInstance {
	id: string
	name: string
	attributes: Record<string, unknown>
	/** Translatable attribute values per language code: `{ de: { text: 'Hallo' } }` (missing = default's). */
	translations?: Record<string, Record<string, unknown>> | null
	/** Per translation: hash of the default text it was made from (`core/translatable.ts` sourceHash; outdated check). */
	translationSources?: Record<string, Record<string, string>> | null
	innerBlocks: BlockInstance[]
}

export interface BlockContext {
	block: BlockInstance
	siblings: BlockInstance[]
	index: number
	parentId: string | null
}

// API shapes come straight from the generated client (exact contract, see api-backend/CLAUDE.md).
export type {
	PageStatus,
	PageSummary,
	PageDetail,
	PublicPage,
	PageTextTranslation,
	MediaRef,
	MediaItem,
	ConfigAddress,
	ConfigEntry,
	ConfigEntryValue,
	ConfigFieldType,
	ConfigFieldDef,
	ConfigGroupDef,
	ConfigPresetDef,
	SiteConfigSchema,
	InstanceConfig,
	FooterLink,
	SiteConfigResponse as SiteConfig,
	TagUsage,
	MenuItem,
	MenuDetail,
	PublicMenu,
	PublicMenuItem,
} from '../../../api/types.gen'
