import { defineBlock } from '../../core/defineBlock'
import meta from './block.json'
import ListItemEdit from './ListItemEdit.vue'
import ListItemIcon from './ListItemIcon.vue'

/** Editor definition (registry). The public render component is `ListItemView.vue`, found by `core/blockViews.ts`. */
export default defineBlock(meta, {
	icon: ListItemIcon,
	attributes: {
		text: { type: 'string', default: '' },
	},
	edit: ListItemEdit,
})
