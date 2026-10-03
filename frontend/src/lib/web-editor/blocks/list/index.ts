import { defineBlock } from '../../core/defineBlock'
import meta from './block.json'
import ListEdit from './ListEdit.vue'
import ListIcon from './ListIcon.vue'
import ListSettings from './ListSettings.vue'

/** Editor definition (registry). The public render component is `ListView.vue`, found by `core/blockViews.ts`. */
export default defineBlock(meta, {
	icon: ListIcon,
	attributes: {
		ordered: { type: 'boolean', default: false },
		blockWidth: { type: 'string', default: 'default' },
	},
	allowedBlocks: ['list-item'],
	edit: ListEdit,
	settings: ListSettings,
})
