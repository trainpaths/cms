<script setup lang="ts">
import { ref, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '../stores/auth'
import { storeToRefs } from 'pinia'
import { Button, Input, Card, Alert, Link, Form } from '@trainpaths/nb-ui'

/**
 * Password change card (staff: admin `ChangePassword.vue`, customers: `public/screens/UserPassword.vue`). The server
 * revokes every session: `changed` fires a moment after the success message, the parent goes to its login.
 */
defineProps<{ backTo: string }>()
const emit = defineEmits<{ changed: [] }>()

const authStore = useAuthStore()
const { t } = useI18n()
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
		localError.value = t('account.mismatch')
		return
	}

	if (newPassword.value.length < 8) {
		localError.value = t('account.tooShort')
		return
	}

	const success = await authStore.changePassword(currentPassword.value, newPassword.value)
	if (success) {
		successMessage.value = t('account.changed')
		currentPassword.value = ''
		newPassword.value = ''
		confirmPassword.value = ''
		setTimeout(() => emit('changed'), 2000)
	}
}
</script>

<template>
	<Card>
		<div class="flex items-center justify-between mb-24">
			<h1 class="text-2xl font-bold">{{ $t('account.changePassword') }}</h1>
			<Link :to="backTo">{{ $t('account.backToProfile') }}</Link>
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
				:placeholder="$t('account.currentPassword')"
				required
			/>
			<Input
				v-model="newPassword"
				type="password"
				:placeholder="$t('account.newPassword')"
				required
				minlength="8"
			/>
			<Input
				v-model="confirmPassword"
				type="password"
				:placeholder="$t('account.confirmPassword')"
				required
				:invalid="!!confirmPassword && !passwordsMatch"
			/>
			<Alert
				v-if="confirmPassword && !passwordsMatch"
				type="error"
			>
				{{ $t('account.mismatch') }}
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
				{{ loading ? $t('account.submitting') : $t('account.submit') }}
			</Button>
		</Form>
	</Card>
</template>
