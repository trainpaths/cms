import { readFile } from 'node:fs/promises'
import { test, expect, createPage, rowAction, type Page } from '../../fixtures/staff.fixture'

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

	// list: the saved server copy, from the row's "…" menu
	await page.goto('/admin/pages')
	const exportItem = await rowAction(rows(page, title), 'page-export')
	const fromList = await downloaded(page, () => exportItem.click())
	expect(fromList.json.page).toEqual(fromEditor.json.page)

	// import sits in the "+ New Page" row
	await page.getByRole('button', { name: '+ New Page' }).click()
	await expect(page.getByTestId('page-import')).toBeVisible()
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
	await page.getByRole('button', { name: '+ New Page' }).click()
	await page.getByTestId('page-import-file').setInputFiles({
		name: 'other.json',
		mimeType: 'application/json',
		buffer: Buffer.from('{"hello": "world"}'),
	})
	await expect(page.getByText('The file is not a page export.')).toBeVisible()
})

test('the new-page row: icon-only import on phones, cancel closes it', async ({ staffPage: page }) => {
	await page.goto('/admin/pages')
	await page.getByRole('button', { name: '+ New Page' }).click()
	const importButton = page.getByTestId('page-import')
	await expect(importButton).toHaveText('Import', { useInnerText: true })

	await page.setViewportSize({ width: 390, height: 800 })
	await expect(importButton).toBeVisible()
	await expect(importButton).toHaveText('', { useInnerText: true })
	await expect(importButton).toHaveAccessibleName('Import page')

	await page.getByTestId('page-new-cancel').click()
	await expect(importButton).toBeHidden()
})

test('row menu: Edit opens the modal, Duplicate copies, Delete asks first', async ({ staffPage: page }) => {
	const title = await createPage(page)
	const slug = await page.getByTestId('page-slug').inputValue()
	const metaSaved = page.waitForResponse((r) => r.request().method() === 'PUT' && r.ok())
	await page.getByTestId('page-meta-title').fill('Copied meta')
	await page.getByTestId('page-meta-title').press('Enter')
	await metaSaved
	await page.goto('/admin/pages')
	const row = rows(page, title).filter({ hasNotText: '(copy)' })
	// view (a link) + "…"
	await expect(row.getByTestId('page-view')).toBeVisible()
	await expect(row.getByRole('button')).toHaveCount(1)
	await row.getByTestId('page-actions').click()
	await expect(row.getByRole('menuitem')).toHaveText(['Edit', 'Duplicate', 'Export', 'Delete'])
	await page.keyboard.press('Escape')

	await (await rowAction(row, 'page-duplicate')).click()
	const copy = rows(page, `${title} (copy)`)
	await expect(copy).toContainText(`/${slug}-2`)
	await expect(copy).toContainText('Draft')
	await copy.getByRole('link').first().click()
	await expect(page.getByTestId('page-meta-title')).toHaveValue('Copied meta')
	await page.goto('/admin/pages')

	await (await rowAction(row, 'page-edit')).click()
	await expect(page.getByTestId('page-edit-dialog')).toBeVisible()
	await expect(row.getByRole('menu')).toHaveCount(0)
	await page.getByTestId('page-edit-dialog').getByRole('button', { name: 'Cancel' }).click()

	await (await rowAction(row, 'page-delete')).click()
	await page.getByTestId('confirm-ok').click()
	await expect(row).toHaveCount(0)
})
