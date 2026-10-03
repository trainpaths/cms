import { test, expect, createPage, type Page } from '../../fixtures/staff.fixture'

const unique = () => crypto.randomUUID().slice(0, 8)

function row(page: Page, title: string) {
	return page.getByTestId('page-row').filter({ hasText: title })
}

// the editor saves tags right away; a full navigation would abort the request
const tagsSaved = (page: Page) =>
	page.waitForResponse((r) => r.url().endsWith('/tags') && r.request().method() === 'PUT' && r.ok())

async function addTag(scope: ReturnType<Page['getByTestId']>, tag: string) {
	await scope.getByTestId('tag-text').fill(tag)
	await scope.getByTestId('tag-text').press('Enter')
	await expect(scope.getByTestId('tag-chip').filter({ hasText: tag.toLowerCase() })).toBeVisible()
}

test('tags edited in the editor sidebar show in the pages list', async ({ staffPage: page }) => {
	const title = await createPage(page)
	const tag = `E2E-Side-${unique()}`
	const tags = page.getByTestId('page-tags')
	const saved = tagsSaved(page)
	await addTag(tags, tag)
	await saved

	await page.goto('/admin/pages')
	await expect(row(page, title).getByTestId('page-row-tag')).toHaveText([tag.toLowerCase()])

	// back in the editor the tag is still there and can be removed
	await row(page, title).getByRole('link').first().click()
	await expect(tags.getByTestId('tag-chip')).toContainText(tag.toLowerCase())
	const removed = tagsSaved(page)
	await tags.getByRole('button', { name: `Remove tag ${tag.toLowerCase()}` }).click()
	await expect(tags.getByTestId('tag-chip')).toHaveCount(0)
	await removed
	await page.goto('/admin/pages')
	await expect(row(page, title).getByTestId('page-row-tag')).toHaveCount(0)
})

test('list modal: autocomplete, popular tags, filter by tag and search', async ({ staffPage: page }) => {
	const first = await createPage(page, `E2E Tags A ${unique()}`)
	const second = await createPage(page, `E2E Tags B ${unique()}`)
	const tag = `e2e-shared-${unique()}`

	await page.goto('/admin/pages')
	await row(page, first).getByTestId('page-edit').click()
	const dialog = page.getByTestId('page-edit-dialog')
	await addTag(dialog, tag)
	await dialog.getByTestId('page-edit-save').click()
	await expect(dialog).toBeHidden()
	await expect(row(page, first).getByTestId('page-row-tag')).toHaveText([tag])

	// the second page picks the existing tag from the autocomplete
	await row(page, second).getByTestId('page-edit').click()
	await expect(dialog.getByTestId('tag-popular')).toBeVisible()
	await dialog.getByTestId('tag-text').fill(tag.slice(0, 14))
	await dialog.getByTestId('tag-suggestion').filter({ hasText: tag }).click()
	await expect(dialog.getByTestId('tag-chip')).toHaveCount(1)
	await dialog.getByTestId('page-edit-save').click()
	await expect(row(page, second).getByTestId('page-row-tag')).toHaveText([tag])

	// filter by tag → exactly these two; search narrows to one
	await page.getByTestId('page-filter-tag').selectOption(tag)
	await expect(page.getByTestId('page-row')).toHaveCount(2)
	await page.getByTestId('page-filter-search').fill(first)
	await expect(page.getByTestId('page-row')).toHaveCount(1)
	await expect(row(page, first)).toBeVisible()
	await page.getByTestId('page-filter-search').fill(`no match ${unique()}`)
	await expect(page.getByText('No pages match.')).toBeVisible()
	await page.getByRole('button', { name: 'Clear filters' }).click()
	await expect(row(page, second)).toBeVisible()

	// sort: least recently updated puts the newer page after the older one
	await page.getByTestId('page-filter-tag').selectOption(tag)
	const sort = page.getByTestId('page-filter-sort')
	await expect(sort).toHaveAttribute('data-sort', 'updated-desc')
	await sort.click()
	await expect(sort).toHaveAttribute('data-sort', 'updated-asc')
	await expect(page.getByTestId('page-row').first()).toContainText(first)
	await sort.click()
	await expect(page.getByTestId('page-row').first()).toContainText(second)
})

test('tags are single words: Space and comma split, pasted text becomes several tags', async ({ staffPage: page }) => {
	const title = await createPage(page, `E2E Words ${unique()}`)
	await page.goto('/admin/pages')
	await row(page, title).getByTestId('page-edit').click()
	const dialog = page.getByTestId('page-edit-dialog')
	const text = dialog.getByTestId('tag-text')
	const u = unique()

	await text.pressSequentially(`Alpha${u} beta${u},gamma${u}`)
	await expect(dialog.getByTestId('tag-chip')).toHaveCount(2)
	await expect(text).toHaveValue(`gamma${u}`)
	await text.fill(`delta${u}  epsilon${u} `)
	await expect(dialog.getByTestId('tag-chip')).toHaveCount(4)
	await expect(dialog.getByTestId('tag-chip').first()).toContainText(`alpha${u}`)
	await expect(text).toHaveValue('')
})

test('list modal edits slug and publish status', async ({ staffPage: page, browser }) => {
	const title = await createPage(page, `E2E Settings ${unique()}`)
	const slug = `e2e-settings-${unique()}`
	await page.goto('/admin/pages')
	await row(page, title).getByTestId('page-edit').click()
	const dialog = page.getByTestId('page-edit-dialog')

	await dialog.getByTestId('page-edit-slug').fill('Not A Slug!')
	await expect(dialog.getByText('Lowercase letters, digits and single hyphens')).toBeVisible()
	await expect(dialog.getByTestId('page-edit-save')).toBeDisabled()
	await dialog.getByTestId('page-edit-slug').fill(slug)
	await dialog.getByTestId('page-edit-published').check()
	await expect(dialog).toContainText(`Live at /${slug}`)
	await dialog.getByTestId('page-edit-save').click()
	await expect(dialog).toBeHidden()

	await expect(row(page, title)).toContainText('Published')
	await expect(row(page, title)).toContainText(`/${slug}`)
	const visitor = await browser.newPage()
	await visitor.goto(`/${slug}`)
	await expect(visitor.getByRole('heading', { name: title })).toBeVisible()
	await visitor.close()

	// a taken slug is reported and nothing else is applied
	await row(page, title).getByTestId('page-edit').click()
	await dialog.getByTestId('page-edit-slug').fill('home')
	await dialog.getByTestId('page-edit-published').uncheck()
	await dialog.getByTestId('page-edit-save').click()
	await expect(dialog.getByRole('alert')).toContainText('already in use')
	await dialog.getByRole('button', { name: 'Cancel' }).click()
	await expect(row(page, title)).toContainText('Published')
})
