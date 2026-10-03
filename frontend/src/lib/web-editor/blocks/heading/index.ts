import { defineBlock } from '../../core/defineBlock'
import meta from './block.json'
import HeadingEdit from './HeadingEdit.vue'
import HeadingIcon from './HeadingIcon.vue'
import HeadingSettings from './HeadingSettings.vue'

/** Editor definition (registry). The public render component is `HeadingView.vue`, found by `core/blockViews.ts`. */
export default defineBlock(meta, {
	icon: HeadingIcon,
	attributes: {
		text: { type: 'string', default: '' },
		level: { type: 'number', default: 2 },
		blockWidth: { type: 'string', default: 'default' },
		backgroundColor: { type: 'string', default: '' },
		textColor: { type: 'string', default: '' },
	},
	edit: HeadingEdit,
	settings: HeadingSettings,
})
