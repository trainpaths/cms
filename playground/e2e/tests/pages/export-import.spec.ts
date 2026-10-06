import { readFile } from 'node:fs/promises'
import { test, expect, createPage, type Page } from '../../fixtures/staff.fixture'

function rows(page: Page, title: string) {
	return page.getByTestId('page-row').filter({ hasText: title })
}

async function downloaded(page: Page, click: () => Promise<void>) {
	const pending = page.waitForEvent('download')
	await click()
	const download = await pending
	return { name: download.suggestedFilename(), json: JSON.parse(await readFile(await download.path(), 'utf8')) }
}

test('a page downloaded as JSON imports as a new draft', async ({ staffPage: page }) => {
	const title = await createPage(page)
	const slug = await page.getByTestId('page-slug').inputValue()
	const metaSaved = page.waitForResponse((r) => r.request().method() === 'PUT' && r.ok())
	await page.getByTestId('page-meta-title').fill('Exported meta')
	await page.getByTestId('page-meta-title').press('Enter')
	await metaSaved

	// editor: exports what's on screen
	const fromEditor = await downloaded(page, () => page.getByTestId('page-download-json').click())
	expect(fromEditor.name).toBe(`${slug}.json`)
	expect(fromEditor.json).toMatchObject({
		format: 'cms-page',
		version: 1,
		page: { title, slug, metaTitle: 'Exported meta', blocks: [] },
	})

	// list: the saved server copy
	await page.goto('/admin/pages')
	const fromList = await downloaded(page, () => rows(page, title).getByTestId('page-download').click())
	expect(fromList.json.page).toEqual(fromEditor.json.page)

	await page.getByTestId('page-import-file').setInputFiles({
		name: fromList.name,
		mimeType: 'application/json',
		buffer: Buffer.from(JSON.stringify(fromList.json)),
	})
	await expect(page.getByRole('button', { name: 'Back', exact: true })).toBeVisible()
	await expect(page.getByTestId('page-slug')).toHaveValue(`${slug}-2`)
	await expect(page.getByTestId('page-meta-title')).toHaveValue('Exported meta')

	await page.goto('/admin/pages')
	await expect(rows(page, title)).toHaveCount(2)
})

test('importing a file that is not a page export shows an error', async ({ staffPage: page }) => {
	await page.goto('/admin/pages')
	await page.getByTestId('page-import-file').setInputFiles({
		name: 'other.json',
		mimeType: 'application/json',
		buffer: Buffer.from('{"hello": "world"}'),
	})
	await expect(page.getByText('The file is not a page export.')).toBeVisible()
})
