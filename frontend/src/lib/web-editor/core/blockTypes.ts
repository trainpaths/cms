import { registerBlockType } from './blockRegistry'
import type { BlockType } from './types'

// every blocks/<name>/index.ts: adding a block folder is all it takes. `/src/blocks` is root-relative = the
// instance app's own blocks; registered after the built-ins, so the same name replaces a built-in block
const builtIn = import.meta.glob<BlockType>('../blocks/*/index.ts', { eager: true, import: 'default' })
const app = import.meta.glob<BlockType>('/src/blocks/*/index.ts', { eager: true, import: 'default' })

/** Registers the built-in + app blocks (alphabetical by folder). Idempotent (registry is keyed by name); called by the editor. */
export function registerAllBlocks(): void {
	;[...Object.values(builtIn), ...Object.values(app)].forEach(registerBlockType)
}
