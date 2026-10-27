<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import { Button, Input, Card, Alert, Link, Form } from '@trainpaths/nb-ui'

const route = useRoute()
const authStore = useAuthStore()

type Status = 'pending' | 'success' | 'error'
const status = ref<Status>('pending')

const token = computed(() => (typeof route.query.token === 'string' ? route.query.token : ''))

// Resend flow shown when the token is missing/invalid.
const resendEmail = ref('')
const resendLoading = ref(false)
const resendDone = ref(false)

onMounted(async () => {
	if (!token.value) {
		status.value = 'error'
		return
	}
	const success = await authStore.confirmEmail(token.value)
	status.value = success ? 'success' : 'error'
})

async function handleResend() {
	resendLoading.value = true
	await authStore.requestVerification(resendEmail.value)
	resendLoading.value = false
	resendDone.value = true
}
</script>

<template>
	<div class="p-32 max-w-480 mx-auto font-sans">
		<Card>
			<h1 class="text-2xl font-bold mb-24">{{ $t('screens.verifyEmail.title') }}</h1>

			<Alert
				v-if="status === 'pending'"
				type="info"
			>
				{{ $t('screens.verifyEmail.pending') }}
			</Alert>

			<template v-if="status === 'success'">
				<Alert
					type="success"
					class="mb-16"
				>
					{{ $t('screens.verifyEmail.done') }}
				</Alert>
				<Link to="/login">{{ $t('screens.verifyEmail.continue') }}</Link>
			</template>

			<template v-if="status === 'error'">
				<Alert
					type="error"
					class="mb-16"
				>
					{{ $t('screens.verifyEmail.invalid') }}
				</Alert>

				<Alert
					v-if="resendDone"
					type="info"
					class="mb-16"
				>
					{{ $t('screens.verifyEmail.resent') }}
				</Alert>

				<Form
					v-if="!resendDone"
					@submit="handleResend"
				>
					<Input
						v-model="resendEmail"
						type="email"
						:placeholder="$t('screens.verifyEmail.resendEmail')"
						required
					/>
					<Button
						type="submit"
						:loading="resendLoading"
						:disabled="resendLoading"
					>
						{{ resendLoading ? $t('screens.verifyEmail.submitting') : $t('screens.verifyEmail.submit') }}
					</Button>
				</Form>

				<p class="mt-16 text-black opacity-60">
					<Link to="/login">{{ $t('common.backToLogin') }}</Link>
				</p>
			</template>
		</Card>
	</div>
</template>
