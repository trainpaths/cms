<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { storeToRefs } from 'pinia'
import { Button, Input, Card, Alert, Link, Form } from '@trainpaths/nb-ui'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const { error, loading } = storeToRefs(authStore)

const token = computed(() => (typeof route.query.token === 'string' ? route.query.token : ''))

const newPassword = ref('')
const confirmPassword = ref('')
const localError = ref('')
const successMessage = ref('')

const passwordsMatch = computed(() => newPassword.value === confirmPassword.value)
const canSubmit = computed(
	() =>
		!!token.value &&
		newPassword.value &&
		confirmPassword.value &&
		passwordsMatch.value &&
		newPassword.value.length >= 8,
)

async function handleSubmit() {
	localError.value = ''
	successMessage.value = ''

	if (!token.value) {
		localError.value = 'This reset link is missing its token.'
		return
	}
	if (!passwordsMatch.value) {
		localError.value = 'Passwords do not match'
		return
	}
	if (newPassword.value.length < 8) {
		localError.value = 'New password must be at least 8 characters'
		return
	}

	const success = await authStore.resetPassword(token.value, newPassword.value)
	if (success) {
		successMessage.value = 'Password reset successfully. Redirecting to login...'
		newPassword.value = ''
		confirmPassword.value = ''
		setTimeout(() => {
			router.push('/login')
		}, 2000)
	}
}
</script>

<template>
	<div class="p-32 max-w-480 mx-auto font-sans">
		<Card>
			<h1 class="text-2xl font-bold mb-24">Choose a new password</h1>

			<Alert
				v-if="!token"
				type="error"
				class="mb-16"
			>
				This reset link is invalid or incomplete. Request a new one from
				<Link to="/forgot-password">forgot password</Link>.
			</Alert>

			<Alert
				v-if="successMessage"
				type="success"
				class="mb-16"
			>
				{{ successMessage }}
			</Alert>

			<Form @submit="handleSubmit">
				<Input
					v-model="newPassword"
					type="password"
					placeholder="New password (min 8 characters)"
					required
					minlength="8"
				/>
				<Input
					v-model="confirmPassword"
					type="password"
					placeholder="Confirm new password"
					required
					:invalid="!!confirmPassword && !passwordsMatch"
				/>
				<Alert
					v-if="confirmPassword && !passwordsMatch"
					type="error"
				>
					Passwords do not match
				</Alert>
				<Alert
					v-if="localError"
					type="error"
				>
					{{ localError }}
				</Alert>
				<Alert
					v-if="error"
					type="error"
				>
					{{ error }}
				</Alert>
				<Button
					type="submit"
					:loading="loading"
					:disabled="!canSubmit"
				>
					{{ loading ? 'Resetting...' : 'Reset password' }}
				</Button>
			</Form>

			<p class="mt-16 text-black opacity-60">
				<Link to="/login">Back to login</Link>
			</p>
		</Card>
	</div>
</template>
