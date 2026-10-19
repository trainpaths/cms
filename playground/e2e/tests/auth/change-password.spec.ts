import { test, expect } from '@playwright/test'
import { uniqueEmail, testPassword, registerCustomer } from '../../fixtures/test-utils'

async function changePassword(page: import('@playwright/test').Page, current: string, next: string, confirm = next) {
	await page.goto('/user/password')
	await page.getByPlaceholder('Current password').fill(current)
	await page.getByPlaceholder('New password (min 8 characters)').fill(next)
	await page.getByPlaceholder('Confirm new password').fill(confirm)
}

test.describe('Change Password', () => {
	test('successful password change logs out user', async ({ page }) => {
		const password = testPassword()
		await registerCustomer(page, uniqueEmail('change-pw-success'), { password })

		await changePassword(page, password, 'NewPassword456!')
		await page.getByRole('button', { name: 'Change Password' }).click()

		await expect(page.getByText('Password changed successfully')).toBeVisible()
		await expect(page).toHaveURL('/login', { timeout: 5000 })
	})

	test('incorrect current password shows error', async ({ page }) => {
		await registerCustomer(page, uniqueEmail('change-pw-wrong'))

		await changePassword(page, 'WrongPassword123!', 'NewPassword456!')
		await page.getByRole('button', { name: 'Change Password' }).click()

		await expect(page.getByText('Current password is incorrect')).toBeVisible()
		await expect(page).toHaveURL('/user/password')
	})

	test('mismatched passwords shows validation error', async ({ page }) => {
		await registerCustomer(page, uniqueEmail('change-pw-mismatch'))

		await changePassword(page, testPassword(), 'NewPassword456!', 'DifferentPassword789!')

		await expect(page.getByText('Passwords do not match')).toBeVisible()
		await expect(page.getByRole('button', { name: 'Change Password' })).toBeDisabled()
	})

	test('can login with new password after change', async ({ page }) => {
		const email = uniqueEmail('change-pw-login')
		const password = testPassword()
		const newPassword = 'NewPassword456!'
		await registerCustomer(page, email, { password })

		await changePassword(page, password, newPassword)
		await page.getByRole('button', { name: 'Change Password' }).click()
		await expect(page).toHaveURL('/login', { timeout: 5000 })

		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(newPassword)
		await page.getByRole('button', { name: /^Log in$/ }).click()

		await expect(page).toHaveURL('/')
	})

	test('old password no longer works after change', async ({ page }) => {
		const email = uniqueEmail('change-pw-old')
		const password = testPassword()
		await registerCustomer(page, email, { password })

		await changePassword(page, password, 'NewPassword456!')
		await page.getByRole('button', { name: 'Change Password' }).click()
		await expect(page).toHaveURL('/login', { timeout: 5000 })

		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(password)
		await page.getByRole('button', { name: /^Log in$/ }).click()

		await expect(page.getByRole('alert')).toBeVisible()
	})
})
