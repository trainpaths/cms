import pluginVue from 'eslint-plugin-vue'
import { defineConfigWithVueTs, vueTsConfigs } from '@vue/eslint-config-typescript'
import prettier from 'eslint-config-prettier'

// eslint-config-prettier last: turns off rules that clash with Prettier.
export default defineConfigWithVueTs(
	{
		name: 'app/files-to-lint',
		files: ['**/*.{ts,mts,tsx,vue}'],
	},
	{
		name: 'app/files-to-ignore',
		ignores: [
			'**/dist/**',
			'**/dist-ssr/**',
			'frontend/src/api/**',
			'**/playwright-report/**',
			'**/test-results/**',
			'api-backend/**',
		],
	},
	pluginVue.configs['flat/essential'],
	vueTsConfigs.recommended,
	{
		name: 'app/rules',
		rules: {
			// Underscore-prefixed args/vars are intentionally unused.
			'@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_', varsIgnorePattern: '^_' }],
			'vue/multi-word-component-names': 'off',
		},
	},
	prettier,
)
