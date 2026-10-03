import { nanoid } from 'nanoid'
import type { BlockType, BlockInstance } from './types'

const registry = new Map<string, BlockType>()

export function registerBlockType(blockType: BlockType): void {
	registry.set(blockType.name, blockType)
}

export function getBlockType(name: string): BlockType | undefined {
	return registry.get(name)
}

export function getRegisteredBlockTypes(): BlockType[] {
	return Array.from(registry.values())
}

export function createBlockInstance(
	name: string,
	overrides?: Partial<Pick<BlockInstance, 'attributes' | 'innerBlocks'>>,
): BlockInstance {
	const blockType = registry.get(name)
	if (!blockType) {
		throw new Error(`Block type "${name}" is not registered`)
	}

	const defaults: Record<string, string | number | boolean> = {}
	for (const [key, def] of Object.entries(blockType.attributes)) {
		defaults[key] = def.default
	}

	return {
		id: nanoid(),
		name,
		attributes: { ...defaults, ...overrides?.attributes },
		innerBlocks: overrides?.innerBlocks ?? [],
	}
}
