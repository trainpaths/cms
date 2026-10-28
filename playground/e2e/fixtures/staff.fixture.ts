import { test as base, type Locator, type Page, expect } from '@playwright/test'

// Staff accounts can't self-register; the editor specs log in as the bootstrap super admin.
// Locally: set BOOTSTRAP_SUPERADMIN_* in the root .env (docker compose) and export the same
// values when running Playwright. CI sets both (see .github/workflows/ci.yml).
export const staffCredentials = {
	email: process.env.E2E_STAFF_EMAIL ?? process.env.BOOTSTRAP_SUPERADMIN_EMAIL ?? '',
	password: process.env.E2E_STAFF_PASSWORD ?? process.env.BOOTSTRAP_SUPERADMIN_PASSWORD ?? '',
}

type StaffFixtures = {
	staffPage: Page
}

export async function loginAsStaff(page: Page) {
	await page.goto('/admin/login')
	await page.getByPlaceholder('Email').fill(staffCredentials.email)
	await page.getByPlaceholder('Password').fill(staffCredentials.password)
	await page.getByRole('button', { name: /^Log in$/ }).click()
	await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()
}

export const test = base.extend<StaffFixtures>({
	staffPage: async ({ page }, use) => {
		test.skip(
			!staffCredentials.email || !staffCredentials.password,
			'Set BOOTSTRAP_SUPERADMIN_EMAIL/PASSWORD (or E2E_STAFF_*) to run editor specs',
		)
		await loginAsStaff(page)
		await use(page)
	},
})

/** Creates a page through the UI and waits for the editor. Returns the page title used. */
export async function createPage(page: Page, title = `E2E ${crypto.randomUUID().slice(0, 8)}`) {
	await page.goto('/admin/pages')
	await page.getByRole('button', { name: '+ New Page' }).click()
	await page.getByPlaceholder('Page title...').fill(title)
	await page.getByRole('button', { name: 'Create', exact: true }).click()
	await expect(page.getByRole('button', { name: 'Back', exact: true })).toBeVisible()
	return title
}

/** Opens a pages-list row's "…" menu and returns one of its items. */
export async function rowAction(row: Locator, item: 'page-edit' | 'page-duplicate' | 'page-export' | 'page-delete') {
	await row.getByTestId('page-actions').click()
	return row.getByTestId(item)
}

/** Inserts a block from the left sidebar inserter. */
export async function insertBlock(page: Page, title: string) {
	await page
		.getByTestId('inserter-block')
		.filter({ hasText: new RegExp(`^${title}`) })
		.click()
}

// 40×20 red PNG
const TEST_PNG = Buffer.from(
	'iVBORw0KGgoAAAANSUhEUgAAACgAAAAUCAIAAABwJOjsAAAAJElEQVR4nGO4Y2MzIIhh1OJRi0ctHrV41OJRi0ctHrV45FgMABzjJr2Q+ZlYAAAAAElFTkSuQmCC',
	'base64',
)

/**
 * Uploads a PNG through the first media upload input inside `scope` (hidden file input next to an
 * "Upload" button). Returns the unique file name, which the media library shows.
 */
export async function uploadImage(scope: Locator, name = `e2e-${crypto.randomUUID().slice(0, 8)}.png`) {
	await scope.getByTestId('media-upload-input').first().setInputFiles({
		name,
		mimeType: 'image/png',
		buffer: TEST_PNG,
	})
	return name
}

/** Saves pending edits, then publishes the saved copy (editor header; Publish needs a saved page). */
export async function publish(page: Page) {
	const save = page.getByTestId('page-save')
	if (await save.isEnabled()) await save.click()
	// waits for the save to finish: Publish stays disabled while saving
	await page.getByTestId('page-publish').click()
	await page.getByTestId('confirm-ok').click()
	await expect(page.getByTestId('page-unpublish')).toBeVisible()
	await expect(page.getByTestId('page-publish')).toHaveText('Publish changes')
}

/** Editor header Unpublish. */
export async function unpublish(page: Page) {
	await page.getByTestId('page-unpublish').click()
	await page.getByTestId('confirm-ok').click()
	await expect(page.getByTestId('page-publish')).toHaveText('Publish')
	await expect(page.getByTestId('page-unpublish')).toHaveCount(0)
}

/** Editor header language menu: edit the language named `name` ("Deutsch", "English"). */
export async function editLanguage(page: Page, name: string) {
	await page.getByTestId('edit-language').getByRole('button').click()
	await page.getByRole('menuitem', { name: new RegExp(`^${name}`) }).click()
}

export { expect } from '@playwright/test'
