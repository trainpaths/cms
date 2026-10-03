import { test, expect } from '@playwright/test'
import { uniqueEmail, testPassword } from '../../fixtures/test-utils'

test.describe('Customer Registration', () => {
	test('successful registration shows dashboard', async ({ page }) => {
		const email = uniqueEmail('reg-success')

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByPlaceholder('Display name (optional)').fill('Test User')
		await page.getByRole('button', { name: /Register as customer/i }).click()

		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()
		await expect(page.getByText('Welcome, Test User!')).toBeVisible()
	})

	test('duplicate email shows error message', async ({ page }) => {
		const email = uniqueEmail('reg-dup')

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.goto('/profile')
		await page.getByRole('button', { name: 'Logout' }).click()

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

	test('loading state shown during registration', async ({ page }) => {
		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(uniqueEmail('loading'))
		await page.getByPlaceholder('Password').fill(testPassword())

		const submitButton = page.getByRole('button', { name: /Register as customer/i })
		await submitButton.click()

		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible({ timeout: 10000 })
	})
})
