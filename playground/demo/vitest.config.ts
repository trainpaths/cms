import { defineConfig } from 'vitest/config'

// demo/ unit tests (mock API + seed): plain Node, no browser, no Vue plugin needed
export default defineConfig({
	root: import.meta.dirname,
	test: { include: ['**/*.test.ts'] },
})
