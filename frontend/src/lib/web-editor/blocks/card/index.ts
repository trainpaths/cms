import { defineBlock } from '../../core/defineBlock'
import meta from './block.json'
import CardEdit from './CardEdit.vue'
import CardIcon from './CardIcon.vue'
import CardSettings from './CardSettings.vue'

/** Editor definition (registry). The public render component is `CardView.vue`, found by `core/blockViews.ts`. */
export default defineBlock(meta, {
	icon: CardIcon,
	attributes: {
		title: { type: 'string', default: '' },
		description: { type: 'string', default: '' },
		mediaId: { type: 'string', default: '' },
		blockWidth: { type: 'string', default: 'default' },
		backgroundColor: { type: 'string', default: '' },
		textColor: { type: 'string', default: '' },
	},
	allowedBlocks: ['link'],
	edit: CardEdit,
	settings: CardSettings,
})
