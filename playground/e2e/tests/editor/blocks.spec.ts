import { test, expect, createPage, insertBlock, uploadImage } from '../../fixtures/staff.fixture'

test.beforeEach(async ({ staffPage: page }) => {
	await createPage(page)
})

const main = (page: import('@playwright/test').Page) => page.locator('main')

test.describe('Block operations', () => {
	test('adds a card, edits its title and nests a link', async ({ staffPage: page }) => {
		await insertBlock(page, 'Card')
		await page.getByPlaceholder('Click to add title...').fill('My Card Title')
		await expect(page.getByPlaceholder('Click to add title...')).toHaveValue('My Card Title')

		// Card's inner inserter only offers Link (allowedBlocks)
		const inner = main(page).locator('.inner-blocks-container')
		await expect(inner.getByRole('button', { name: /^\+ / })).toHaveText(['+ Link'])
		await inner.getByRole('button', { name: '+ Link' }).click()
		await expect(page.getByPlaceholder('Click to add link...')).toBeVisible()
	})

	test("sidebar insert respects allowedBlocks of the selected block's parent", async ({ staffPage: page }) => {
		await insertBlock(page, 'Card')
		await main(page).locator('.inner-blocks-container').getByRole('button', { name: '+ Link' }).click()
		await page.getByPlaceholder('Click to add link...').click()

		// A Link inside the Card is selected; a Heading can't go in the Card, so it lands after it.
		await insertBlock(page, 'Heading')
		await expect(
			main(page).locator('.inner-blocks-container').getByPlaceholder('Click to add heading...'),
		).toHaveCount(0)
		await expect(page.getByPlaceholder('Click to add heading...')).toHaveCount(1)
	})

	test('heading text and level', async ({ staffPage: page }) => {
		await insertBlock(page, 'Heading')
		const input = page.getByPlaceholder('Click to add heading...')
		await input.fill('Hello')
		await expect(input).toHaveValue('Hello')
		// toolbar shows only the current level; the others are in its dropdown
		await page.getByTitle('Heading level: Heading 2').click()
		await page.getByRole('option', { name: 'H4' }).click()
		await expect(page.getByTitle('Heading level: Heading 4')).toBeVisible()
		await expect(input).toHaveClass(/text-xl/)
	})

	test('paragraph text and alignment', async ({ staffPage: page }) => {
		await insertBlock(page, 'Paragraph')
		const text = page.getByPlaceholder('Click to add text...')
		await text.fill('Some text')
		await page.getByTitle('Alignment: Align left').click()
		await page.getByRole('option', { name: 'Align center' }).click()
		await expect(text).toHaveClass(/text-center/)
	})

	test('list with items, toggled to numbered', async ({ staffPage: page }) => {
		await insertBlock(page, 'List')
		const inner = main(page).locator('.inner-blocks-container')
		await expect(inner.getByRole('button', { name: /^\+ / })).toHaveText(['+ List Item'])
		await inner.getByRole('button', { name: '+ List Item' }).click()
		await inner.getByRole('button', { name: '+ List Item' }).click()
		await expect(main(page).getByText('Click to add item...')).toHaveCount(2)

		// The list is still selected, so its inline toolbar (list type) is showing.
		await page.locator('select').selectOption('numbered')
		await expect(main(page).getByText('2.', { exact: true })).toBeVisible()
	})

	test('image: upload, then reuse via "Choose existing"', async ({ staffPage: page }) => {
		await insertBlock(page, 'Image')
		const name = await uploadImage(main(page))
		const first = main(page).locator('figure img').first()
		await expect(first).toBeVisible()
		const src = await first.getAttribute('src')

		await insertBlock(page, 'Image')
		await main(page).getByTestId('media-choose').last().click()
		const picker = page.getByTestId('media-picker')
		await picker.getByTestId('media-item').filter({ hasText: name }).click()
		await expect(picker).toBeHidden()
		await expect(main(page).locator('figure img')).toHaveCount(2)
		await expect(main(page).locator('figure img').last()).toHaveAttribute('src', src!)
	})

	test('card image via upload', async ({ staffPage: page }) => {
		await insertBlock(page, 'Card')
		await uploadImage(main(page))
		await expect(main(page).locator('img.rounded-t-lg')).toHaveAttribute('src', /^\/api\/public\/media\//)
	})

	test('remove, duplicate and move', async ({ staffPage: page }) => {
		await insertBlock(page, 'Heading')
		await page.getByPlaceholder('Click to add heading...').fill('First')
		await page.getByTitle('Duplicate').click()
		await expect(page.getByPlaceholder('Click to add heading...')).toHaveCount(2)

		// Duplicate is selected and sits second; move it up.
		await page.getByPlaceholder('Click to add heading...').nth(1).fill('Second')
		await page.getByTitle('Move up').nth(1).click()
		await expect(page.getByPlaceholder('Click to add heading...').first()).toHaveValue('Second')

		await page.getByTitle('Remove').first().click()
		await expect(page.getByPlaceholder('Click to add heading...')).toHaveCount(1)
	})
})

test.describe('History and keyboard', () => {
	test('undo is disabled without history, then undo/redo an insert', async ({ staffPage: page }) => {
		const undo = page.getByTitle('Undo (Ctrl+Z)')
		const redo = page.getByTitle('Redo (Ctrl+Shift+Z)')
		await expect(undo).toBeDisabled()

		await insertBlock(page, 'Heading')
		await expect(page.getByPlaceholder('Click to add heading...')).toHaveCount(1)
		await undo.click()
		await expect(page.getByPlaceholder('Click to add heading...')).toHaveCount(0)
		await redo.click()
		await expect(page.getByPlaceholder('Click to add heading...')).toHaveCount(1)
	})

	test('keyboard undo/redo: Ctrl+Z, Ctrl+Shift+Z and Ctrl+Y', async ({ staffPage: page }) => {
		const headings = page.getByPlaceholder('Click to add heading...')
		await insertBlock(page, 'Heading')
		await page.keyboard.press('Escape')
		await expect(headings).toHaveCount(1)
		await page.keyboard.press('ControlOrMeta+z')
		await expect(headings).toHaveCount(0)
		await page.keyboard.press('ControlOrMeta+Shift+z')
		await expect(headings).toHaveCount(1)
		await page.keyboard.press('ControlOrMeta+z')
		await expect(headings).toHaveCount(0)
		await page.keyboard.press('Control+y')
		await expect(headings).toHaveCount(1)
	})

	test('Escape deselects the block', async ({ staffPage: page }) => {
		await insertBlock(page, 'Heading')
		await expect(page.getByTestId('sidebar-tab-block')).toHaveAttribute('aria-selected', 'true')
		await page.keyboard.press('Escape')
		await expect(page.getByTestId('sidebar-tab-page')).toHaveAttribute('aria-selected', 'true')
	})
})

test('content persists across reloads', async ({ staffPage: page }) => {
	await insertBlock(page, 'Heading')
	await page.getByPlaceholder('Click to add heading...').fill('Persisted heading')
	await page.getByRole('button', { name: 'Save', exact: true }).click()
	await expect(page.getByText('Saved')).toBeVisible()

	await page.reload()
	await expect(page.getByPlaceholder('Click to add heading...')).toHaveValue('Persisted heading')
})
