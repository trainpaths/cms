<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { loginRouteFor } from '../router'
import { storeToRefs } from 'pinia'
import { Button, Input, Card, Alert, Link, Form } from '@trainpaths/nb-ui'

const router = useRouter()
const authStore = useAuthStore()
const { error, loading } = storeToRefs(authStore)

const currentPassword = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const successMessage = ref('')
const localError = ref('')

const passwordsMatch = computed(() => newPassword.value === confirmPassword.value)
const canSubmit = computed(
	() =>
		currentPassword.value &&
		newPassword.value &&
		confirmPassword.value &&
		passwordsMatch.value &&
		newPassword.value.length >= 8,
)

async function handleSubmit() {
	localError.value = ''
	successMessage.value = ''

	if (!passwordsMatch.value) {
		localError.value = 'Passwords do not match'
		return
	}

	if (newPassword.value.length < 8) {
		localError.value = 'New password must be at least 8 characters'
		return
	}

	const type = authStore.authType
	const success = await authStore.changePassword(currentPassword.value, newPassword.value)
	if (success) {
		successMessage.value = 'Password changed successfully. Please log in again.'
		currentPassword.value = ''
		newPassword.value = ''
		confirmPassword.value = ''
		setTimeout(() => {
			router.push(loginRouteFor(type))
		}, 2000)
	}
}
</script>

<template>
	<div class="p-32 max-w-480 mx-auto font-sans">
		<Card>
			<div class="flex items-center justify-between mb-24">
				<h1 class="text-2xl font-bold">Change Password</h1>
				<Link to="/profile">Back to Profile</Link>
			</div>

			<Alert
				v-if="successMessage"
				type="success"
				class="mb-16"
			>
				{{ successMessage }}
			</Alert>

			<Form @submit="handleSubmit">
				<Input
					v-model="currentPassword"
					type="password"
					placeholder="Current password"
					required
				/>
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
					{{ loading ? 'Changing...' : 'Change Password' }}
				</Button>
			</Form>
		</Card>
	</div>
</template>
