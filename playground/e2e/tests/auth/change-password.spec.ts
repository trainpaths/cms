import { test, expect } from '@playwright/test'
import { uniqueEmail, testPassword } from '../../fixtures/test-utils'

test.describe('Change Password', () => {
	test('successful password change logs out user', async ({ page }) => {
		const email = uniqueEmail('change-pw-success')
		const password = testPassword()
		const newPassword = 'NewPassword456!'

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(password)
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.goto('/change-password')
		await page.getByPlaceholder('Current password').fill(password)
		await page.getByPlaceholder('New password (min 8 characters)').fill(newPassword)
		await page.getByPlaceholder('Confirm new password').fill(newPassword)
		await page.getByRole('button', { name: 'Change Password' }).click()

		await expect(page.getByText('Password changed successfully')).toBeVisible()
		await expect(page).toHaveURL('/login', { timeout: 5000 })
	})

	test('incorrect current password shows error', async ({ page }) => {
		const email = uniqueEmail('change-pw-wrong')
		const password = testPassword()

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(password)
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.goto('/change-password')
		await page.getByPlaceholder('Current password').fill('WrongPassword123!')
		await page.getByPlaceholder('New password (min 8 characters)').fill('NewPassword456!')
		await page.getByPlaceholder('Confirm new password').fill('NewPassword456!')
		await page.getByRole('button', { name: 'Change Password' }).click()

		await expect(page.getByText('Current password is incorrect')).toBeVisible()
		await expect(page).toHaveURL('/change-password')
	})

	test('mismatched passwords shows validation error', async ({ page }) => {
		const email = uniqueEmail('change-pw-mismatch')

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.goto('/change-password')
		await page.getByPlaceholder('Current password').fill(testPassword())
		await page.getByPlaceholder('New password (min 8 characters)').fill('NewPassword456!')
		await page.getByPlaceholder('Confirm new password').fill('DifferentPassword789!')

		await expect(page.getByText('Passwords do not match')).toBeVisible()
		await expect(page.getByRole('button', { name: 'Change Password' })).toBeDisabled()
	})

	test('can login with new password after change', async ({ page }) => {
		const email = uniqueEmail('change-pw-login')
		const password = testPassword()
		const newPassword = 'NewPassword456!'

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(password)
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.goto('/change-password')
		await page.getByPlaceholder('Current password').fill(password)
		await page.getByPlaceholder('New password (min 8 characters)').fill(newPassword)
		await page.getByPlaceholder('Confirm new password').fill(newPassword)
		await page.getByRole('button', { name: 'Change Password' }).click()

		await expect(page).toHaveURL('/login', { timeout: 5000 })

		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(newPassword)
		await page.getByRole('button', { name: /^Log in$/ }).click()

		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()
	})

	test('old password no longer works after change', async ({ page }) => {
		const email = uniqueEmail('change-pw-old')
		const password = testPassword()
		const newPassword = 'NewPassword456!'

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(password)
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.goto('/change-password')
		await page.getByPlaceholder('Current password').fill(password)
		await page.getByPlaceholder('New password (min 8 characters)').fill(newPassword)
		await page.getByPlaceholder('Confirm new password').fill(newPassword)
		await page.getByRole('button', { name: 'Change Password' }).click()

		await expect(page).toHaveURL('/login', { timeout: 5000 })

		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(password)
		await page.getByRole('button', { name: /^Log in$/ }).click()

		await expect(page.getByRole('alert')).toBeVisible()
	})
})
