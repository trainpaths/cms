import { defineBlock } from '../../core/defineBlock'
import meta from './block.json'
import ImageEdit from './ImageEdit.vue'
import ImageIcon from './ImageIcon.vue'
import ImageSettings from './ImageSettings.vue'

/** Editor definition (registry). The public render component is `ImageView.vue`, found by `core/blockViews.ts`. */
export default defineBlock(meta, {
	icon: ImageIcon,
	attributes: {
		// alt lives on the media object, not the block
		mediaId: { type: 'string', default: '' },
		caption: { type: 'string', default: '' },
		width: { type: 'number', default: 100 },
		blockWidth: { type: 'string', default: 'default' },
	},
	edit: ImageEdit,
	settings: ImageSettings,
})
