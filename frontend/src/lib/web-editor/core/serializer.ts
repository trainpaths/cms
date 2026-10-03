import type { BlockInstance } from './types'

export function serialize(blocks: BlockInstance[]): string {
	return JSON.stringify(blocks, null, 2)
}

export function parse(json: string): BlockInstance[] {
	const data = JSON.parse(json)
	if (!Array.isArray(data)) {
		throw new Error('Invalid block data: expected an array')
	}
	return data as BlockInstance[]
}
