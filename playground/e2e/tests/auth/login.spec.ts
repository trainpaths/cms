import { test, expect } from '@playwright/test'
import { uniqueEmail, testPassword, registerCustomer, loginCustomer, logoutCustomer } from '../../fixtures/test-utils'

test.describe('Customer Login', () => {
	test('successful login returns home', async ({ page }) => {
		const email = uniqueEmail('login-valid')
		await registerCustomer(page, email)
		await logoutCustomer(page)

		await loginCustomer(page, email)
	})

	test('returns to the page the visitor came from, without a full page load', async ({ page }) => {
		const email = uniqueEmail('login-return')
		await registerCustomer(page, email)
		await logoutCustomer(page)

		await page.goto('/privacy-policy')
		await expect(page.getByRole('heading', { name: 'Privacy Policy', level: 1 })).toBeVisible()
		const documents: string[] = []
		page.on('request', (req) => {
			if (req.resourceType() === 'document') documents.push(req.url())
		})

		const loginLink = page.getByTestId('site-header').getByRole('link', { name: 'Login' }).first()
		test.skip((await loginLink.count()) === 0, 'main menu has no Login item (menus.spec replaces it)')
		await loginLink.click()
		await expect(page).toHaveURL(/\/login\?redirect=/)
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByRole('button', { name: /^Log in$/ }).click()

		await expect(page).toHaveURL('/privacy-policy')
		await expect(page.getByRole('heading', { name: 'Privacy Policy', level: 1 })).toBeVisible()
		expect(documents).toEqual([])
	})

	test('header shows Account while signed in, Login after logout', async ({ page }) => {
		await page.goto('/')
		const header = page.getByTestId('site-header')
		test.skip((await header.getByRole('link', { name: 'Login' }).count()) === 0, 'main menu has no Login item')
		await registerCustomer(page, uniqueEmail('login-header'))
		await expect(header.getByRole('link', { name: 'Account' }).first()).toBeVisible()
		await expect(header.getByRole('link', { name: 'Login' })).toHaveCount(0)

		await header.getByRole('link', { name: 'Account' }).first().click()
		await expect(page).toHaveURL('/user/profile')
		await page.getByRole('button', { name: 'Logout' }).click()
		await expect(page).toHaveURL('/')
		await expect(header.getByRole('link', { name: 'Login' }).first()).toBeVisible()
	})

	test('invalid password shows error', async ({ page }) => {
		const email = uniqueEmail('login-badpw')
		await registerCustomer(page, email)
		await logoutCustomer(page)

		await page.goto('/login')
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
