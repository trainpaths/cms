import { defineBlock } from '../../core/defineBlock'
import meta from './block.json'
import LinkEdit from './LinkEdit.vue'
import LinkIcon from './LinkIcon.vue'
import LinkSettings from './LinkSettings.vue'

/** Editor definition (registry). The public render component is `LinkView.vue`, found by `core/blockViews.ts`. */
export default defineBlock(meta, {
	icon: LinkIcon,
	attributes: {
		label: { type: 'string', default: '' },
		url: { type: 'string', default: '' },
		textColor: { type: 'string', default: '' },
	},
	edit: LinkEdit,
	settings: LinkSettings,
})
