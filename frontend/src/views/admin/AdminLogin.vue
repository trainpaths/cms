<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import { storeToRefs } from 'pinia'
import { Button, Input, Card, Alert, Form, Link } from '@trainpaths/nb-ui'

// Staff (site owner) login. No register / forgot-password links: staff accounts are created by a
// super admin and the reset flow is customer-only.
const router = useRouter()
const authStore = useAuthStore()
const { error, loading } = storeToRefs(authStore)

const email = ref('')
const password = ref('')

authStore.clearError()

async function handleSubmit() {
	const success = await authStore.login('staff', email.value, password.value)
	if (success) {
		const redirect = authStore.consumeRedirectAfterLogin()
		router.push(redirect ?? { name: 'dashboard' })
	}
}
</script>

<template>
	<div class="p-32 max-w-480 mx-auto font-sans">
		<Card>
			<h1 class="text-2xl font-bold mb-24">Admin Login</h1>

			<Form @submit="handleSubmit">
				<Input
					v-model="email"
					type="email"
					placeholder="Email"
				/>
				<Input
					v-model="password"
					type="password"
					placeholder="Password"
				/>
				<Alert
					v-if="error"
					type="error"
				>
					{{ error }}
				</Alert>
				<Button
					type="submit"
					:disabled="loading"
					:loading="loading"
				>
					{{ loading ? 'Logging in...' : 'Log in' }}
				</Button>
			</Form>

			<p class="mt-16 text-black opacity-60">
				<Link to="/">Back</Link>
			</p>
		</Card>
	</div>
</template>
