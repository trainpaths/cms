import { test, expect, createPage, insertBlock, uploadImage } from '../../fixtures/staff.fixture'
import type { Page } from '@playwright/test'

test.beforeEach(async ({ staffPage: page }) => {
	await createPage(page)
})

const main = (page: Page) => page.locator('main')
const sidebar = (page: Page) => page.locator('aside').last()
const section = (page: Page, title: string) =>
	sidebar(page).locator('div.border-b', { has: page.getByText(title, { exact: true }) })

test.describe('Block editor fixes', () => {
	test('only the drag handle is draggable, not the block', async ({ staffPage: page }) => {
		await insertBlock(page, 'Paragraph')
		await expect(main(page).locator('[draggable="true"]')).toHaveCount(1)
		await expect(page.getByTestId('block-drag-handle')).toHaveAttribute('draggable', 'true')
	})

	test('paragraph grows with its content', async ({ staffPage: page }) => {
		await insertBlock(page, 'Paragraph')
		const text = page.getByPlaceholder('Click to add text...')
		const before = (await text.boundingBox())!.height
		await text.fill('line\nline\nline\nline\nline')
		await expect.poll(async () => (await text.boundingBox())!.height).toBeGreaterThan(before * 2)
	})

	test('paragraph text colour applies', async ({ staffPage: page }) => {
		await insertBlock(page, 'Paragraph')
		const text = page.getByPlaceholder('Click to add text...')
		await section(page, 'Text Color').getByPlaceholder('Custom...').fill('#ff0000')
		await expect(text).toHaveCSS('color', 'rgb(255, 0, 0)')
	})

	test('card background and text colour apply to the card', async ({ staffPage: page }) => {
		await insertBlock(page, 'Card')
		await section(page, 'Background Color').getByPlaceholder('Custom...').fill('#00ff00')
		await section(page, 'Text Color').getByPlaceholder('Custom...').fill('#0000ff')
		const card = main(page).locator('[data-block-id] div.rounded-lg.shadow-xs').first()
		await expect(card).toHaveCSS('background-color', 'rgba(0, 0, 0, 0)')
		await expect(page.getByPlaceholder('Click to add title...')).toHaveCSS('color', 'rgb(0, 0, 255)')
	})

	test('image settings in sidebar sync with toolbar; alt comes from the media object', async ({
		staffPage: page,
	}) => {
		await insertBlock(page, 'Image')
		await uploadImage(section(page, 'Image'))
		const img = main(page).locator('figure img')
		await expect(img).toHaveAttribute('src', /^\/api\/public\/media\/[0-9a-f]{32}\.png$/)
		// with an image set: one "Replace" button, the choices behind it
		await expect(section(page, 'Image').getByTestId('media-choose')).toBeHidden()
		await section(page, 'Image').getByTestId('media-replace').click()
		await expect(section(page, 'Image').getByRole('button', { name: 'Upload new' })).toBeVisible()
		await expect(section(page, 'Image').getByTestId('media-choose')).toBeVisible()
		await page.getByTestId('sidebar-tab-block').click()
		await expect(section(page, 'Image').getByTestId('media-choose')).toBeHidden()
		await section(page, 'Image').getByLabel('Width', { exact: true }).fill('50')
		await section(page, 'Image').getByLabel('Width', { exact: true }).press('Enter')
		await expect(main(page).getByLabel('Width', { exact: true })).toHaveValue('50')

		await expect(img).toHaveAttribute('alt', 'image')
		// caption is no longer an alt fallback
		await page.getByPlaceholder('Add caption...').fill('A caption')
		await expect(img).toHaveAttribute('alt', 'image')
		const alt = section(page, 'Image').getByTestId('media-alt')
		await alt.fill('A red rectangle')
		await alt.blur()
		await expect(img).toHaveAttribute('alt', 'A red rectangle')
		await expect(img).toHaveClass(/mx-auto/)
	})

	test('list type from sidebar', async ({ staffPage: page }) => {
		await insertBlock(page, 'List')
		// freshly inserted list is selected
		await section(page, 'List type').getByRole('button', { name: 'Numbered' }).click()
		await expect(main(page).locator('select')).toHaveValue('numbered')
		await main(page).locator('.inner-blocks-container').getByRole('button', { name: '+ List Item' }).click()
		await expect(main(page).getByText('1.', { exact: true })).toBeVisible()
	})

	test('link url editable in sidebar', async ({ staffPage: page }) => {
		await insertBlock(page, 'Card')
		await main(page).locator('.inner-blocks-container').getByRole('button', { name: '+ Link' }).click()
		await page.getByPlaceholder('Click to add link...').click()
		await section(page, 'Link').getByPlaceholder('https://...').fill('https://example.com')
		await expect(main(page).locator('input[type="url"]')).toHaveValue('https://example.com')
	})
})
