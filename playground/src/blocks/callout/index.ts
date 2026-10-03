import { defineBlock } from '@trainpaths/cms/editor'
import meta from './block.json'
import CalloutEdit from './CalloutEdit.vue'
import CalloutIcon from './CalloutIcon.vue'

/** Instance block: same folder format as the built-ins; the public view is `CalloutView.vue`. */
export default defineBlock(meta, {
	icon: CalloutIcon,
	attributes: {
		text: { type: 'string', default: '' },
		tone: { type: 'string', default: 'info' },
		blockWidth: { type: 'string', default: 'default' },
	},
	edit: CalloutEdit,
})
