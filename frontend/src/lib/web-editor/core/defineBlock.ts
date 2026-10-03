import type { BlockCategory, BlockType } from './types'

/** What a block's `block.json` holds (category is a plain string in JSON). */
type BlockMeta = Pick<BlockType, 'name' | 'title' | 'description' | 'supports' | 'parent'> & {
	category: string
}

/** Editor definition of a block = its `block.json` + components/attributes from `blocks/<name>/index.ts`. */
export function defineBlock(meta: BlockMeta, definition: Omit<BlockType, keyof BlockMeta | 'category'>): BlockType {
	return { ...meta, category: meta.category as BlockCategory, ...definition }
}
