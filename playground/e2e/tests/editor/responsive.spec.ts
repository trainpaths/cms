import type { Page } from '@playwright/test'
import { test, expect, createPage, insertBlock } from '../../fixtures/staff.fixture'

const main = (page: Page) => page.locator('main')
const leftSidebar = (page: Page) => page.locator('aside').filter({ has: page.getByTestId('sidebar-tab-blocks') })
const rightSidebar = (page: Page) => page.locator('aside').filter({ has: page.getByTestId('sidebar-close-right') })

test.describe('Editor layout ≥ md', () => {
	test('canvas takes the space left by both sidebars, no overlap or overflow', async ({ staffPage: page }) => {
		await page.setViewportSize({ width: 1024, height: 800 })
		await createPage(page)
		await insertBlock(page, 'Card')

		const [left, canvas, right] = await Promise.all([
			leftSidebar(page).boundingBox(),
			main(page).boundingBox(),
			rightSidebar(page).boundingBox(),
		])
		expect(left!.x + left!.width).toBeLessThanOrEqual(canvas!.x + 1)
		expect(canvas!.x + canvas!.width).toBeLessThanOrEqual(right!.x + 1)
		expect(await main(page).evaluate((el) => el.scrollWidth <= el.clientWidth)).toBe(true)

		// closing a sidebar gives the canvas its width
		await page.getByTestId('toggle-right-sidebar').click()
		await expect(page.getByTestId('toggle-right-sidebar')).toHaveAttribute('aria-pressed', 'false')
		expect((await main(page).boundingBox())!.width).toBeGreaterThan(canvas!.width)
	})

	test('canvas keeps 320px; sidebars then overlap it instead of overflowing the page', async ({
		staffPage: page,
	}) => {
		await page.setViewportSize({ width: 800, height: 700 })
		await createPage(page)

		const canvas = (await main(page).boundingBox())!
		expect(canvas.width).toBeGreaterThanOrEqual(320)
		const [left, right] = await Promise.all([
			leftSidebar(page).locator('> div').boundingBox(),
			rightSidebar(page).locator('> div').boundingBox(),
		])
		// panels keep their 280px and lie over the canvas edges, inside the window
		expect(left!.width).toBe(280)
		expect(left!.x + left!.width).toBeGreaterThan(canvas.x)
		expect(right!.x).toBeLessThan(canvas.x + canvas.width)
		expect(right!.x + right!.width).toBeLessThanOrEqual(800)
		expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(800)
	})

	test('right sidebar Page/Block tabs follow the selection', async ({ staffPage: page }) => {
		await page.setViewportSize({ width: 1280, height: 800 })
		await createPage(page)
		const pageTab = page.getByTestId('sidebar-tab-page')
		const blockTab = page.getByTestId('sidebar-tab-block')

		await expect(pageTab).toHaveAttribute('aria-selected', 'true')
		await blockTab.click()
		await expect(page.getByText('No block selected')).toBeVisible()

		await insertBlock(page, 'Heading')
		await expect(blockTab).toHaveAttribute('aria-selected', 'true')
		await pageTab.click()
		await expect(page.getByTestId('page-slug')).toBeVisible()

		// X closes the sidebar, selection stays
		await page.getByTestId('sidebar-close-right').click()
		await expect(rightSidebar(page)).toHaveCount(0)
		await expect(main(page).locator('.ring-cms-primary')).toHaveCount(1)
	})
})

test.describe('Editor layout < md', () => {
	test.beforeEach(async ({ staffPage: page }) => {
		await page.setViewportSize({ width: 375, height: 700 })
		await createPage(page)
		// store picks its sidebar defaults on load
		await page.reload()
	})

	test('sidebars are full-screen modals, one at a time, closed with X', async ({ staffPage: page }) => {
		await expect(page.locator('aside')).toHaveCount(0)

		await page.getByTestId('toggle-left-sidebar').click()
		const box = (await leftSidebar(page).boundingBox())!
		expect(box.width).toBe(375)

		await page.getByTestId('toggle-right-sidebar').click()
		await expect(leftSidebar(page)).toHaveCount(0)
		await expect(rightSidebar(page)).toBeVisible()

		await page.getByTestId('sidebar-close-right').click()
		await expect(page.locator('aside')).toHaveCount(0)

		await page.getByTestId('toggle-left-sidebar').click()
		await page.getByTestId('sidebar-close-left').click()
		await expect(page.locator('aside')).toHaveCount(0)
	})

	test('inserting closes the modal; selected block toolbar sits in the top bar', async ({ staffPage: page }) => {
		const bar = page.getByTestId('mobile-block-toolbar')

		await page.getByTestId('toggle-left-sidebar').click()
		await insertBlock(page, 'Heading')
		await expect(page.locator('aside')).toHaveCount(0)
		await expect(bar.getByTestId('block-drag-handle')).toBeVisible()
		// inline toolbar (heading level) teleports along
		await expect(bar.getByTitle(/^Heading level/)).toBeVisible()

		await page.getByTestId('toggle-left-sidebar').click()
		await insertBlock(page, 'Paragraph')
		await bar.getByTitle('Move up').click()
		await expect(
			main(page).locator('[data-block-id]').first().getByPlaceholder('Click to add text...'),
		).toBeVisible()

		await bar.getByTitle('Remove').click()
		await expect(main(page).locator('[data-block-id]')).toHaveCount(1)
		await expect(bar).toBeHidden()
		expect(await main(page).evaluate((el) => el.scrollWidth <= el.clientWidth)).toBe(true)
	})
})
