import { chromium, type FullConfig } from '@playwright/test'

export default async function globalSetup(config: FullConfig) {
    const baseURL = config.projects[0].use.baseURL || 'http://localhost:5173'

    const browser = await chromium.launch()
    const page = await browser.newPage()

    try {
        const response = await page.goto(baseURL, { timeout: 5000 })
        if (!response?.ok()) {
            throw new Error(`Frontend not accessible at ${baseURL}`)
        }
    } catch (error) {
        throw new Error(
            `E2E tests require full stack running.\n` +
                `Run at the repo root: pnpm docker:up\n` +
                `Original error: ${error}`
        )
    } finally {
        await browser.close()
    }
}
