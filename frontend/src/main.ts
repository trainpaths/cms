import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { client } from './api/client.gen'
import { setupInterceptors } from './interceptors'
import { useAuthStore } from './stores/auth'
import { useInstanceStore } from './stores/instance'

/**
 * Boots the admin SPA into `#app`. The instance's `src/main.ts` imports its stylesheet (which imports
 * `@trainpaths/cms/style.css`), then calls this.
 */
export async function createAdmin(): Promise<void> {
	// Same-origin /api (Vite proxy in dev, nginx in container) so SameSite=Strict refresh cookie is sent.
	client.setConfig({ baseUrl: '' })

	const app = createApp(App)
	app.use(createPinia())

	// Session (refresh cookie) + instance config before the first route guard runs.
	await Promise.all([useAuthStore().ensureSession(), useInstanceStore().load()])

	app.use(router)
	setupInterceptors(router)
	app.mount('#app')
}
