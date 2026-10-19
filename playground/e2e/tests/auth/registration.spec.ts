import { test, expect } from '@playwright/test'
import { uniqueEmail, testPassword, registerCustomer, logoutCustomer } from '../../fixtures/test-utils'

test.describe('Customer Registration', () => {
	test('successful registration signs in and returns home', async ({ page }) => {
		await registerCustomer(page, uniqueEmail('reg-success'), { displayName: 'Test User' })

		await expect(page.getByRole('heading', { name: 'Home', level: 1 })).toBeVisible()
		await page.goto('/user/profile')
		await expect(page.getByText('Test User')).toBeVisible()
	})

	test('duplicate email shows error message', async ({ page }) => {
		const email = uniqueEmail('reg-dup')
		await registerCustomer(page, email)
		await logoutCustomer(page)

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByRole('button', { name: /Register as customer/i }).click()

		await expect(page.getByRole('alert')).toBeVisible()
	})

	test('password too short shows error', async ({ page }) => {
		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(uniqueEmail('short-pw'))
		await page.getByPlaceholder('Password').fill('short')
		await page.getByRole('button', { name: /Register as customer/i }).click()

		await expect(page.getByRole('alert')).toBeVisible()
	})
})
