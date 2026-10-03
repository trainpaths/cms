import { defineBlock } from '../../core/defineBlock'
import meta from './block.json'
import ParagraphEdit from './ParagraphEdit.vue'
import ParagraphIcon from './ParagraphIcon.vue'
import ParagraphSettings from './ParagraphSettings.vue'

/** Editor definition (registry). The public render component is `ParagraphView.vue`, found by `core/blockViews.ts`. */
export default defineBlock(meta, {
	icon: ParagraphIcon,
	attributes: {
		text: { type: 'string', default: '' },
		alignment: { type: 'string', default: 'left' },
		blockWidth: { type: 'string', default: 'default' },
		backgroundColor: { type: 'string', default: '' },
		textColor: { type: 'string', default: '' },
	},
	edit: ParagraphEdit,
	settings: ParagraphSettings,
})
