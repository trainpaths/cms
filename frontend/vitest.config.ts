import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'
import { nbUi } from '@trainpaths/nb-ui/vite'

// SSR render tests of the public app; nb-ui ships raw .vue, so it's inlined
export default defineConfig({
	plugins: [vue(), nbUi()],
	ssr: { noExternal: ['@trainpaths/nb-ui'] },
	// what cms() defines from cms.config.json: a localized site, like the playground
	define: {
		__CMS_LANGUAGES__: JSON.stringify(['en-US', 'de-CH', 'fr-CH', 'it-CH']),
		__VUE_I18N_FULL_INSTALL__: true,
		__VUE_I18N_LEGACY_API__: false,
		__INTLIFY_PROD_DEVTOOLS__: false,
	},
})
