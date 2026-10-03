import { test, expect, createPage, insertBlock, uploadImage } from '../../fixtures/staff.fixture'
import type { Page } from '@playwright/test'

test.beforeEach(async ({ staffPage: page }) => {
	await createPage(page)
})

const main = (page: Page) => page.locator('main')
const inserter = (page: Page, title: string) =>
	page.getByTestId('inserter-block').filter({ hasText: new RegExp(`^${title}`) })
const settings = (page: Page) => page.locator('aside').last()

test.describe('Editor UX', () => {
	test('dragging a block from the sidebar creates it at the drop spot', async ({ staffPage: page }) => {
		await insertBlock(page, 'Heading')
		await inserter(page, 'Paragraph').dragTo(main(page).locator('[data-block-id]').first(), {
			targetPosition: { x: 40, y: 4 },
		})
		const blocks = main(page).locator('[data-block-id]')
		await expect(blocks).toHaveCount(2)
		// upper half of the heading → lands before it
		await expect(blocks.first().getByPlaceholder('Click to add text...')).toBeVisible()
	})

	test('dragging onto an empty page creates the first block', async ({ staffPage: page }) => {
		await inserter(page, 'Heading').dragTo(main(page).getByText('Start adding blocks from the sidebar'))
		await expect(main(page).locator('[data-block-id]')).toHaveCount(1)
		await expect(main(page).getByPlaceholder('Click to add heading...')).toBeVisible()
	})

	test('dropping anywhere below the last block appends at the bottom', async ({ staffPage: page }) => {
		await insertBlock(page, 'Heading')
		await page.keyboard.press('Escape')
		const box = (await main(page).boundingBox())!
		await inserter(page, 'Paragraph').dragTo(main(page), {
			targetPosition: { x: box.width / 2, y: box.height - 20 },
		})
		const blocks = main(page).locator('[data-block-id]')
		await expect(blocks).toHaveCount(2)
		await expect(blocks.last().getByPlaceholder('Click to add text...')).toBeVisible()
	})

	test('the first block can move down one spot', async ({ staffPage: page }) => {
		await insertBlock(page, 'Heading')
		await insertBlock(page, 'Paragraph')
		const blocks = main(page).locator('[data-block-id]')
		await blocks.first().hover()
		const handle = (await blocks.first().getByTestId('block-drag-handle').boundingBox())!
		const second = (await blocks.nth(1).boundingBox())!
		// gradual path: a one-step jump leaves the hover-only toolbar before Chromium starts the drag
		await page.mouse.move(handle.x + 8, handle.y + 8)
		await page.mouse.down()
		// lower half of the paragraph → after it
		await page.mouse.move(handle.x + 8, second.y + second.height - 4, { steps: 10 })
		await page.mouse.up()
		await expect(blocks.first().getByPlaceholder('Click to add text...')).toBeVisible()
		await expect(blocks.last().getByPlaceholder('Click to add heading...')).toBeVisible()
	})

	test('drop indicator does not shift the blocks', async ({ staffPage: page }) => {
		await insertBlock(page, 'Heading')
		await insertBlock(page, 'Paragraph')
		await page.keyboard.press('Escape')
		const second = main(page).locator('[data-block-id]').nth(1)
		const before = (await second.boundingBox())!.y
		await inserter(page, 'List').hover()
		await page.mouse.down()
		await second.hover({ position: { x: 40, y: 4 } })
		await second.hover({ position: { x: 44, y: 6 } })
		await expect(page.getByTestId('drop-indicator')).toHaveCount(1)
		expect((await second.boundingBox())!.y).toBe(before)
		await page.mouse.up()
	})

	test('drop indicator is a 4px line with 3px space each side', async ({ staffPage: page }) => {
		await insertBlock(page, 'Heading')
		await insertBlock(page, 'Paragraph')
		await page.keyboard.press('Escape')
		const second = main(page).locator('[data-block-id]').nth(1)
		await inserter(page, 'List').hover()
		await page.mouse.down()
		await second.hover({ position: { x: 40, y: 4 } })
		await second.hover({ position: { x: 44, y: 6 } })
		const line = page.getByTestId('drop-indicator')
		await expect(line).toHaveCSS('height', '4px')
		const slot = (await line.locator('..').boundingBox())!
		const box = (await line.boundingBox())!
		expect(box.y - slot.y).toBe(3)
		expect(slot.y + slot.height - (box.y + box.height)).toBe(3)
		await page.mouse.up()
	})

	test('wrapper adds no padding; the card has no inner border', async ({ staffPage: page }) => {
		await insertBlock(page, 'Card')
		const wrapper = main(page).locator('[data-block-id]').first()
		const card = wrapper.locator('.rounded-lg.shadow-xs').first()
		const outer = (await wrapper.boundingBox())!
		const inner = (await card.boundingBox())!
		expect(inner.x).toBe(outer.x)
		expect(inner.width).toBe(outer.width)
		await expect(card).toHaveCSS('border-top-width', '0px')
	})

	test('no drop indicator over a container that rejects the block', async ({ staffPage: page }) => {
		await insertBlock(page, 'Card')
		const cardContent = main(page).locator('.inner-blocks-container')

		await inserter(page, 'Paragraph').hover()
		await page.mouse.down()
		await cardContent.hover({ position: { x: 20, y: 30 } })
		await cardContent.hover({ position: { x: 24, y: 34 } })
		await expect(page.getByTestId('drop-indicator')).toHaveCount(0)
		await page.mouse.up()
		await expect(main(page).getByPlaceholder('Click to add text...')).toHaveCount(0)
	})

	test('click-insert scrolls the new block into view', async ({ staffPage: page }) => {
		for (let i = 0; i < 12; i++) await insertBlock(page, 'Heading')
		await page.keyboard.press('Escape') // deselect → append at the end
		expect(await main(page).evaluate((el) => el.scrollHeight > el.clientHeight)).toBe(true)
		await main(page).evaluate((el) => el.scrollTo(0, 0))
		await insertBlock(page, 'Paragraph')
		await expect(main(page).getByPlaceholder('Click to add text...')).toBeInViewport()
	})

	test('paragraph alignment and heading level in the sidebar', async ({ staffPage: page }) => {
		await insertBlock(page, 'Paragraph')
		await settings(page).getByTitle('Align right').click()
		await expect(page.getByPlaceholder('Click to add text...')).toHaveClass(/text-right/)
		await expect(page.getByTitle('Alignment: Align right')).toBeVisible()

		await insertBlock(page, 'Heading')
		await settings(page).getByRole('button', { name: 'H1', exact: true }).click()
		await expect(page.getByPlaceholder('Click to add heading...')).toHaveClass(/text-4xl/)
	})

	test('image width is a percent number', async ({ staffPage: page }) => {
		await insertBlock(page, 'Image')
		await uploadImage(main(page))
		await expect(main(page).locator('figure img')).toBeVisible()
		const width = main(page).getByLabel('Width', { exact: true })
		await expect(width).toHaveAttribute('type', 'number')
		await expect(width).toHaveValue('100')
		await width.fill('40')
		await width.press('Enter')
		await expect(main(page).locator('figure img')).toHaveAttribute('style', /width: 40%/)
		await expect(settings(page).getByLabel('Width slider')).toHaveValue('40')
	})
})
