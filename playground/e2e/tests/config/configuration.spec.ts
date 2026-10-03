import { test, expect, createPage, uploadImage, type Page } from '../../fixtures/staff.fixture'

// Site config is one site-wide row: each test writes its full state, never relies on leftovers.
// Shape: core field firmName + group contact; the playground's cms.config.json adds field vatId + group socials.

async function openConfiguration(page: Page) {
	await page.goto('/admin/configuration')
	await expect(page.getByTestId('config-fields')).toBeVisible()
}

function group(page: Page, key: string) {
	return page.getByTestId(`config-group-${key}`)
}

/** Removes every entry of a group (none of the playground's groups has required entries). */
async function clearGroup(page: Page, key: string) {
	const entries = group(page, key).getByTestId('config-entry')
	while ((await entries.count()) > 0) await entries.first().getByTestId('config-entry-remove').click()
}

async function addCustom(page: Page, groupKey: string, name: string, value: string, type = 'text') {
	const entries = group(page, groupKey).getByTestId('config-entry')
	await group(page, groupKey).getByTestId('config-add-custom').click()
	const entry = entries.last()
	await entry.getByTestId('config-entry-key').fill(name)
	await entry.getByTestId('config-entry-type').selectOption(type)
	await entry.getByTestId('config-entry-value').fill(value)
}

async function save(page: Page) {
	await page.getByTestId('config-save').click()
	await expect(page.getByText('Configuration saved')).toBeVisible()
}

/** Creates and publishes an empty page, returns a visitor (separate context) on it. */
async function publishedPageVisitor(page: Page, browser: import('@playwright/test').Browser) {
	const slug = `e2e-${crypto.randomUUID().slice(0, 8)}`
	await createPage(page)
	await page.getByTestId('page-slug').fill(slug)
	await page.getByTestId('page-slug').press('Enter')
	await page.getByTestId('publish-toggle').click()
	await expect(page.getByTestId('publish-toggle')).toHaveText('Unpublish')
	const visitor = await (await browser.newContext()).newPage()
	await visitor.goto(`/${slug}`)
	return visitor
}

test('firm name, contact entries, logo and icon persist and render in the public footer', async ({
	staffPage: page,
	browser,
}) => {
	await openConfiguration(page)
	await page.getByTestId('config-field-firmName').fill('ACME E2E GmbH')
	await clearGroup(page, 'contact')
	const contact = group(page, 'contact')
	await contact.getByTestId('config-add-address').click()
	await contact.getByTestId('config-address-street').fill('Main St 1')
	await contact.getByTestId('config-address-postal').fill('12345')
	await contact.getByTestId('config-address-city').fill('Town')
	await contact.getByTestId('config-add-phone').click()
	await contact.getByTestId('config-entry').last().getByTestId('config-entry-value').fill('+49 123 456')
	await contact.getByTestId('config-add-email').click()
	await contact.getByTestId('config-entry').last().getByTestId('config-entry-value').fill('hello@acme.test')
	await addCustom(page, 'contact', 'Opening hours', 'Mon–Fri 9–17')
	await uploadImage(page.getByTestId('config-logo'))
	await expect(page.getByTestId('config-logo-preview')).toBeVisible()
	await uploadImage(page.getByTestId('config-icon'))
	await expect(page.getByTestId('config-icon-preview')).toBeVisible()
	await save(page)

	await page.reload()
	const entries = group(page, 'contact').getByTestId('config-entry')
	await expect(entries).toHaveCount(4)
	await expect(entries.nth(0).getByTestId('config-entry-label')).toHaveText('Address')
	await expect(entries.nth(0).getByTestId('config-address-street')).toHaveValue('Main St 1')
	await expect(entries.nth(3).getByTestId('config-entry-key')).toHaveValue('Opening hours')
	await expect(page.getByTestId('config-field-firmName')).toHaveValue('ACME E2E GmbH')
	const iconUrl = await page.getByTestId('config-icon-preview').getAttribute('src')

	// admin keeps the CMS icon
	await expect(page.locator('link[rel="icon"]')).toHaveAttribute('href', '/favicon.svg')

	const visitor = await publishedPageVisitor(page, browser)
	const footer = visitor.getByTestId('site-footer')
	await expect(footer.getByTestId('site-footer-logo')).toBeVisible()
	await expect(footer.getByTestId('site-footer-firm')).toHaveText('ACME E2E GmbH')
	await expect(footer.getByText('12345 Town')).toBeVisible()
	await expect(footer.getByRole('link', { name: '+49 123 456' })).toHaveAttribute('href', 'tel:+49123456')
	await expect(footer.getByRole('link', { name: 'hello@acme.test' })).toHaveAttribute('href', 'mailto:hello@acme.test')
	await expect(footer.getByText('Opening hours')).toBeVisible()
	await expect(visitor.locator('link[rel="icon"]')).toHaveAttribute('href', iconUrl!)
})

test('duplicate entry names block saving, removed entries and logo disappear from the footer', async ({
	staffPage: page,
	browser,
}) => {
	await openConfiguration(page)
	await clearGroup(page, 'contact')
	await addCustom(page, 'contact', 'Fax', '1', 'phone')
	await addCustom(page, 'contact', 'fax', '2', 'phone')
	await expect(page.getByText('Name is used twice.')).toBeVisible()
	await expect(page.getByTestId('config-save')).toBeDisabled()

	await clearGroup(page, 'contact')
	await addCustom(page, 'contact', 'Only entry', 'Just this')
	await page.getByTestId('config-field-firmName').fill('')
	if (await page.getByTestId('config-logo-remove').isVisible()) await page.getByTestId('config-logo-remove').click()
	await save(page)

	const visitor = await publishedPageVisitor(page, browser)
	const footer = visitor.getByTestId('site-footer')
	await expect(footer.getByText('Just this')).toBeVisible()
	await expect(footer.getByTestId('site-footer-firm')).toHaveCount(0)
	await expect(footer.getByTestId('site-footer-logo')).toHaveCount(0)
})

test('instance field and group (vatId, socials) show on the imprint template page', async ({ staffPage: page }) => {
	await openConfiguration(page)
	await expect(group(page, 'socials').getByTestId('config-add-custom')).toHaveCount(0)
	await page.getByTestId('config-field-vatId').fill('DE123456789')
	await clearGroup(page, 'socials')
	const socials = group(page, 'socials')
	await socials.getByTestId('config-add-instagram').click()
	await socials.getByTestId('config-entry').last().getByTestId('config-entry-value').fill('https://instagram.com/acme')
	await socials.getByTestId('config-add-linkedin').click()
	await socials.getByTestId('config-entry').last().getByTestId('config-entry-value').fill('https://linkedin.com/company/acme')
	await expect(socials.getByTestId('config-add-instagram')).toHaveCount(0)
	await save(page)

	await page.goto('/imprint')
	const details = page.getByTestId('imprint-details')
	await expect(details.getByText('DE123456789')).toBeVisible()
	await expect(details.getByRole('link', { name: 'Instagram' })).toHaveAttribute('href', 'https://instagram.com/acme')
	await expect(details.getByRole('link', { name: 'LinkedIn' })).toBeVisible()
})
