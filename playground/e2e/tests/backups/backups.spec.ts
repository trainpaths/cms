import { test, expect, createPage, loginAsStaff, staffCredentials, type Page } from '../../fixtures/staff.fixture'

// One serial flow: the restore replaces the database (and ends every session), so nothing may run in between.
test.describe.configure({ mode: 'serial' })

async function openBackups(page: Page) {
	await page.goto('/profile?tab=backups')
	await expect(page.getByTestId('backup-settings')).toBeVisible()
}

function rows(page: Page) {
	return page.getByTestId('backup-row')
}

test('schedule, back up now, download', async ({ staffPage: page }) => {
	await page.goto('/profile')
	await page.getByRole('tab', { name: 'Backups' }).click()
	await expect(page).toHaveURL(/tab=backups/)
	// compose stack without BACKUP_DIR (CI, default .env)
	await expect(page.getByTestId('backup-location')).toHaveText('Developer has not set backup folder')

	await page.getByTestId('backup-interval').selectOption('weekly')
	await expect(page.getByTestId('backup-weekday')).toBeVisible()
	await page.getByTestId('backup-interval').selectOption('monthly')
	await expect(page.getByTestId('backup-weekday')).toBeHidden()
	await page.getByTestId('backup-day-of-month').selectOption('15')
	await page.getByTestId('backup-time').fill('04:00')
	await page.getByTestId('backup-settings-save').click()
	await expect(page.getByText('Backup schedule saved')).toBeVisible()
	await expect(page.getByTestId('backup-next-run')).toContainText('15')
	await page.reload()
	await expect(page.getByTestId('backup-day-of-month')).toHaveValue('15')

	const before = await rows(page).count()
	await page.getByTestId('backup-create').click()
	await expect(rows(page)).toHaveCount(before + 1, { timeout: 60_000 })
	const newest = rows(page).first()
	await expect(newest.getByTestId('backup-kind')).toHaveText('Manual')

	const pending = page.waitForEvent('download')
	await newest.getByTestId('backup-download').click()
	expect((await pending).suggestedFilename()).toMatch(/^manual-\d{8}-\d{6}(-\d+)?\.tar\.gz$/)

	// leave the schedule off for the rest of the suite
	await page.getByTestId('backup-interval').selectOption('off')
	await page.getByTestId('backup-settings-save').click()
	await expect(page.getByTestId('backup-next-run')).toHaveText('Off')
})

test('restore brings back the backed-up state and signs out', async ({ staffPage: page }) => {
	await openBackups(page)
	await page.getByTestId('backup-create').click()
	await expect(page.getByText(/Backup manual-.* created/)).toBeVisible({ timeout: 60_000 })
	const backupName = (await rows(page).first().locator('.font-mono').textContent())!.trim()

	// made after the backup: gone after the restore
	const marker = await createPage(page, `E2E Restore marker ${crypto.randomUUID().slice(0, 8)}`)

	await openBackups(page)
	await rows(page).filter({ hasText: backupName }).getByTestId('backup-restore').click()
	await page.getByTestId('confirm-ok').click()
	await expect(page).toHaveURL(/\/admin\/login/, { timeout: 120_000 })
	await expect(page.getByText(/Backup restored/)).toBeVisible()

	await loginAsStaff(page)
	await page.goto('/admin/pages')
	await expect(page.getByTestId('page-row').first()).toBeVisible()
	await expect(page.getByTestId('page-row').filter({ hasText: marker })).toHaveCount(0)

	await openBackups(page)
	await expect(rows(page).filter({ hasText: 'Before restore' }).first()).toBeVisible()
})

test('staff without super admin role see no Backups tab', async ({ page, request }) => {
	test.skip(!staffCredentials.email, 'needs the bootstrap super admin to create the account')
	const login = await request.post('/api/auth/staff/login', {
		data: { email: staffCredentials.email, password: staffCredentials.password },
	})
	const { accessToken } = await login.json()
	const email = `e2e-editor-${crypto.randomUUID().slice(0, 8)}@test.local`
	const password = 'e2e-editor-password'
	const created = await request.post('/api/auth/staff/register', {
		headers: { Authorization: `Bearer ${accessToken}` },
		data: { email, password, displayName: 'E2E Editor', roles: ['staff'] },
	})
	expect(created.ok()).toBeTruthy()

	await page.goto('/admin/login')
	await page.getByPlaceholder('Email').fill(email)
	await page.getByPlaceholder('Password').fill(password)
	await page.getByRole('button', { name: /^Log in$/ }).click()
	await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

	await page.goto('/profile?tab=backups')
	await expect(page.getByText(email)).toBeVisible()
	await expect(page.getByRole('tab', { name: 'Backups' })).toHaveCount(0)
	const editor = await (await request.post('/api/auth/staff/login', { data: { email, password } })).json()
	const denied = await request.get('/api/backups', { headers: { Authorization: `Bearer ${editor.accessToken}` } })
	expect(denied.status()).toBe(403)
})
