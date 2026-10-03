import { defineConfig } from '@hey-api/openapi-ts'

// throwOnError: SDK calls resolve with non-optional `data` and throw ApiError (see src/interceptors.ts)
export default defineConfig({
	input: './openapi/swagger.json',
	output: 'src/api',
	plugins: [
		'@hey-api/typescript',
		'@hey-api/sdk',
		{ name: '@hey-api/client-fetch', throwOnError: true },
	],
})
