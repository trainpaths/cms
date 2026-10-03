import { test, expect } from '@playwright/test'
import { uniqueEmail, testPassword } from '../../fixtures/test-utils'

test.describe('Customer Login', () => {
	test('successful login with valid credentials', async ({ page }) => {
		const email = uniqueEmail('login-valid')
		const password = testPassword()

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(password)
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.goto('/profile')
		await page.getByRole('button', { name: 'Logout' }).click()

		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(password)
		await page.getByRole('button', { name: /^Log in$/ }).click()

		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()
	})

	test('invalid password shows error', async ({ page }) => {
		const email = uniqueEmail('login-badpw')

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.goto('/profile')
		await page.getByRole('button', { name: 'Logout' }).click()

		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill('WrongPassword123!')
		await page.getByRole('button', { name: /^Log in$/ }).click()

		await expect(page.getByRole('alert')).toBeVisible()
	})

	test('non-existent user shows error', async ({ page }) => {
		await page.goto('/login')
		await page.getByPlaceholder('Email').fill('nobody@test.local')
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByRole('button', { name: /^Log in$/ }).click()

		await expect(page.getByRole('alert')).toBeVisible()
	})

	test('customer login has no staff toggle', async ({ page }) => {
		await page.goto('/login')
		await expect(page.getByRole('button', { name: 'Staff' })).toHaveCount(0)
	})

	test('staff login lives at /admin/login and rejects unknown users', async ({ page }) => {
		await page.goto('/admin/login')
		await expect(page.getByRole('heading', { name: 'Admin Login' })).toBeVisible()
		await page.getByPlaceholder('Email').fill('nobody@test.local')
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByRole('button', { name: /^Log in$/ }).click()

		await expect(page.getByRole('alert')).toBeVisible()
	})

	test('admin login back link returns to /', async ({ page }) => {
		await page.goto('/admin/login')
		await page.getByRole('link', { name: 'Back' }).click()
		await expect(page).toHaveURL('/')
	})
})
