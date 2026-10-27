<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { I18nT, useI18n } from 'vue-i18n'
import { useAuthStore } from '../../stores/auth'
import { storeToRefs } from 'pinia'
import { Button, Input, Card, Alert, Link, Form } from '@trainpaths/nb-ui'

const route = useRoute()
const { t } = useI18n()
const router = useRouter()
const authStore = useAuthStore()
const { error, loading } = storeToRefs(authStore)

// the stored HTML has no query: read the token only after hydration
const mounted = ref(false)
onMounted(() => (mounted.value = true))
const token = computed(() => (mounted.value && typeof route.query.token === 'string' ? route.query.token : ''))

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
		localError.value = t('screens.resetPassword.missingToken')
		return
	}
	if (!passwordsMatch.value) {
		localError.value = t('account.mismatch')
		return
	}
	if (newPassword.value.length < 8) {
		localError.value = t('account.tooShort')
		return
	}

	const success = await authStore.resetPassword(token.value, newPassword.value)
	if (success) {
		successMessage.value = t('screens.resetPassword.done')
		newPassword.value = ''
		confirmPassword.value = ''
		setTimeout(() => {
			router.push({ name: 'login' })
		}, 2000)
	}
}
</script>

<template>
	<div class="p-32 max-w-480 mx-auto font-sans">
		<Card>
			<h1 class="text-2xl font-bold mb-24">{{ $t('screens.resetPassword.title') }}</h1>

			<Alert
				v-if="mounted && !token"
				type="error"
				class="mb-16"
			>
				<I18nT
					keypath="screens.resetPassword.invalidLink"
					scope="global"
				>
					<template #link>
						<Link to="/forgot-password">{{ $t('screens.resetPassword.invalidLinkLink') }}</Link>
					</template>
				</I18nT>
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
					{{ loading ? $t('screens.resetPassword.submitting') : $t('screens.resetPassword.submit') }}
				</Button>
			</Form>

			<p class="mt-16 text-black opacity-60">
				<Link to="/login">{{ $t('common.backToLogin') }}</Link>
			</p>
		</Card>
	</div>
</template>
