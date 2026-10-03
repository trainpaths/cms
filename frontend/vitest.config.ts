import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'
import { nbUi } from '@trainpaths/nb-ui/vite'

// SSR render tests of the public app; nb-ui ships raw .vue, so it's inlined
export default defineConfig({
	plugins: [vue(), nbUi()],
	ssr: { noExternal: ['@trainpaths/nb-ui'] },
})
