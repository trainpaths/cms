import type { Component } from 'vue'

export interface AttributeDefinition {
	type: 'string' | 'number' | 'boolean'
	default: string | number | boolean
}

export interface BlockSupports {
	width?: boolean
	backgroundColor?: boolean
	textColor?: boolean
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
	MenuSummary,
	MenuDetail,
	PublicMenu,
	PublicMenuItem,
} from '../../../api/types.gen'
