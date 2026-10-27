import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'
import { nbUi } from '@trainpaths/nb-ui/vite'

// what cms() defines from cms.config.json
const define = (languages: string[]) => ({
	__CMS_LANGUAGES__: JSON.stringify(languages),
	__VUE_I18N_FULL_INSTALL__: true,
	__VUE_I18N_LEGACY_API__: false,
	__INTLIFY_PROD_DEVTOOLS__: false,
})

// SSR render tests of the public app; nb-ui ships raw .vue, so it's inlined
export default defineConfig({
	plugins: [vue(), nbUi()],
	ssr: { noExternal: ['@trainpaths/nb-ui'] },
	test: {
		projects: [
			// a localized site, like the playground
			{
				extends: true,
				define: define(['en-US', 'de-CH', 'fr-CH', 'it-CH']),
				test: { name: 'localized', include: ['src/**/*.test.ts'], exclude: ['src/**/*.single.test.ts'] },
			},
			// the regression to guard: a site without localization behaves as before
			{
				extends: true,
				define: define(['en']),
				test: { name: 'single-language', include: ['src/**/*.single.test.ts'] },
			},
		],
	},
})
