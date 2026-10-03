import { defineConfig } from 'vite'
import { cms } from '@trainpaths/cms/vite'

export default defineConfig({
	plugins: [cms()],
	// .env lives at the repo root (shared with docker compose)
	envDir: '../',
})
