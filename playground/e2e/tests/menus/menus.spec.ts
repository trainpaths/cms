import { test, expect, createPage, type Page } from '../../fixtures/staff.fixture'

// The main menu is one site-wide row: the test replaces all of its items.

const unique = () => crypto.randomUUID().slice(0, 8)

/** Creates + publishes a page; returns its title and slug. */
async function publishedPage(page: Page, label: string) {
	const title = await createPage(page, `E2E Menu ${label} ${unique()}`)
	await page.getByTestId('publish-toggle').click()
	await expect(page.getByTestId('publish-toggle')).toHaveText('Unpublish')
	const slug = await page.getByTestId('page-slug').inputValue()
	return { title, slug }
}

async function openMain(page: Page) {
	await page.goto('/admin/menus')
	await expect(page.getByTestId('menu-list-item').first()).toContainText('Main')
	await expect(page.getByTestId('menu-handle')).toHaveText('main')
}

async function clearItems(page: Page) {
	const rows = page.getByTestId('menu-item')
	while ((await rows.count()) > 0) await rows.first().getByTestId('menu-item-remove').first().click()
}

async function addFromPicker(page: Page, title: string) {
	const picker = page.getByTestId('menu-page-picker')
	await picker.getByTestId('page-filter-search').fill(title)
	await picker.getByTestId('menu-picker-page').filter({ hasText: title }).getByTestId('menu-picker-add').click()
	await picker.getByTestId('page-filter-search').fill('')
}

const rows = (page: Page) => page.getByTestId('menu-item-row')

async function addItem(page: Page, label: string, link?: string) {
	await page.getByTestId('menu-add-item').click()
	await page.getByTestId('menu-item-label').last().fill(label)
	if (link) await page.getByTestId('menu-item-link').last().fill(link)
}

async function dragPage(page: Page, title: string, target: ReturnType<Page['getByTestId']>) {
	const picker = page.getByTestId('menu-page-picker')
	await picker.getByTestId('page-filter-search').fill(title)
	await picker
		.getByTestId('menu-picker-page')
		.filter({ hasText: title })
		.dragTo(target, { targetPosition: { x: 200, y: 20 } })
	await picker.getByTestId('page-filter-search').fill('')
}

test('build the main menu and see it as the public navigation', async ({ staffPage: page, browser }) => {
	const about = await publishedPage(page, 'About')
	const team = await publishedPage(page, 'Team')

	await openMain(page)
	await clearItems(page)
	// click + on a page: label = title, link = its path
	await addFromPicker(page, about.title)
	await expect(page.getByTestId('menu-item-label').first()).toHaveValue(about.title)
	await expect(page.getByTestId('menu-item-link').first()).toHaveValue(`/${about.slug}`)

	// an item without link groups its children; Team is dragged from the picker onto its middle
	await addItem(page, 'More')
	await dragPage(page, team.title, rows(page).nth(1))
	await expect(page.locator('[data-testid="menu-item"][data-depth="2"]')).toHaveCount(1)

	await addItem(page, 'Docs', 'ftp://nope')
	await expect(page.getByText('Enter a path like /about or an http(s) URL.')).toBeVisible()
	await expect(page.getByTestId('menu-save')).toBeDisabled()
	await page.getByTestId('menu-item-link').last().fill('https://example.com/docs')
	// Docs → into "More" by dragging its grip onto the row's middle
	await page
		.getByTestId('menu-item-grip')
		.last()
		.dragTo(rows(page).nth(1), { targetPosition: { x: 200, y: 20 } })

	await expect(page.getByTestId('menu-item')).toHaveCount(4)
	await expect(page.locator('[data-testid="menu-item"][data-depth="2"]')).toHaveCount(2)

	await page.getByTestId('menu-save').click()
	await expect(page.getByText('Menu saved')).toBeVisible()
	await page.reload()
	await expect(page.locator('[data-testid="menu-item"][data-depth="2"]')).toHaveCount(2)

	// public site, desktop: top level About + More, dropdown with Team + Docs
	const visitor = await browser.newPage()
	await visitor.goto(`/${about.slug}`)
	const nav = visitor.getByTestId('site-nav')
	await expect(nav.getByTestId('site-nav-item')).toHaveCount(2)
	await expect(nav.getByTestId('site-nav-item').first()).toContainText(about.title)
	const more = nav.getByTestId('site-nav-item').nth(1)
	await more.hover()
	const dropdown = more.getByTestId('site-nav-dropdown')
	await expect(dropdown).toBeVisible()
	await expect(dropdown.getByRole('link', { name: 'Docs' })).toHaveAttribute('href', 'https://example.com/docs')
	await dropdown.getByRole('link', { name: team.title }).click()
	await expect(visitor).toHaveURL(new RegExp(`/${team.slug}$`))

	// mobile: burger panel shows the whole tree
	await visitor.setViewportSize({ width: 375, height: 800 })
	await expect(visitor.getByTestId('site-nav')).toBeHidden()
	await visitor.getByTestId('site-nav-burger').click()
	const panel = visitor.getByTestId('site-nav-mobile')
	await expect(panel.getByRole('link', { name: team.title })).toBeVisible()
	await expect(panel.getByRole('link', { name: 'Docs' })).toBeVisible()
	await visitor.close()

	// unpublishing a page drops it from the public menu
	await page.goto('/admin/pages')
	await page.getByTestId('page-row').filter({ hasText: team.title }).getByRole('link').first().click()
	await page.getByTestId('publish-toggle').click()
	await expect(page.getByTestId('publish-toggle')).toHaveText('Publish')
	const later = await browser.newPage()
	await later.goto(`/${about.slug}`)
	await later.getByTestId('site-nav-item').nth(1).hover()
	await expect(later.getByTestId('site-nav-dropdown').getByRole('link', { name: 'Docs' })).toBeVisible()
	await expect(later.getByRole('link', { name: team.title })).toHaveCount(0)
	await later.close()
})

test('extra menus can be created and deleted, the main menu cannot', async ({ staffPage: page }) => {
	await openMain(page)
	await expect(page.getByTestId('menu-delete')).toHaveCount(0)

	const id = unique()
	const handle = `e2e-footer-${id}`
	await page.getByTestId('menu-new').click()
	await page.getByTestId('menu-new-handle').fill(`E2E Footer ${id}`)
	await expect(page.getByText(`Saved as ${handle}`)).toBeVisible()
	await page.getByTestId('menu-new-create').click()
	await expect(page.getByTestId('menu-handle')).toHaveText(handle)
	const footer = page.getByTestId('menu-list-item').filter({ hasText: handle })
	await expect(footer).toHaveCount(1)

	// switching menus with changes asks first: Cancel stays, Save saves and switches
	await addItem(page, 'Temp')
	const dialog = page.getByTestId('unsaved-dialog')
	await page.getByTestId('menu-list-item').first().click()
	await expect(dialog).toContainText('Do you want to save the changes?')
	await page.getByTestId('unsaved-cancel').click()
	await expect(dialog).toBeHidden()
	await expect(page.getByTestId('menu-handle')).toHaveText(handle)
	await page.getByTestId('menu-list-item').first().click()
	await page.getByTestId('unsaved-save').click()
	await expect(page.getByTestId('menu-handle')).toHaveText('main')
	await footer.click()
	await expect(page.getByTestId('menu-handle')).toHaveText(handle)
	await expect(page.getByTestId('menu-item-label')).toHaveCount(1)
	await expect(page.getByTestId('menu-item-label')).toHaveValue('Temp')

	// leaving the view: Don't save discards
	await addItem(page, 'Unsaved')
	await page.getByRole('navigation').getByRole('link', { name: 'Pages', exact: true }).click()
	await page.getByTestId('unsaved-discard').click()
	await expect(page).toHaveURL(/\/admin\/pages$/)
	await page.goto('/admin/menus')
	await page.getByTestId('menu-list-item').filter({ hasText: handle }).click()
	await expect(page.getByTestId('menu-handle')).toHaveText(handle)
	await expect(page.getByTestId('menu-item-label')).toHaveCount(1)
	await expect(page.getByTestId('menu-item-label')).toHaveValue('Temp')

	await page.getByTestId('menu-delete').click()
	await page.getByTestId('confirm-ok').click()
	await expect(page.getByTestId('menu-list-item').filter({ hasText: handle })).toHaveCount(0)
	await expect(page.getByTestId('menu-handle')).toHaveText('main')
})
