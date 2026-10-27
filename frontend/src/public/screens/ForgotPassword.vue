<script setup lang="ts">
import { ref } from 'vue'
import { useAuthStore } from '../../stores/auth'
import { storeToRefs } from 'pinia'
import { Button, Input, Card, Alert, Link, Form } from '@trainpaths/nb-ui'

const authStore = useAuthStore()
const { loading } = storeToRefs(authStore)

const email = ref('')
const submitted = ref(false)

async function handleSubmit() {
	await authStore.forgotPassword(email.value)
	// Always show the same confirmation — never reveal whether the account exists.
	submitted.value = true
}
</script>

<template>
	<div class="p-32 max-w-480 mx-auto font-sans">
		<Card>
			<h1 class="text-2xl font-bold mb-24">{{ $t('screens.forgotPassword.title') }}</h1>

			<Alert
				v-if="submitted"
				type="info"
				class="mb-16"
			>
				{{ $t('screens.forgotPassword.sent') }}
			</Alert>

			<Form
				v-if="!submitted"
				@submit="handleSubmit"
			>
				<Input
					v-model="email"
					type="email"
					:placeholder="$t('common.email')"
					required
				/>
				<Button
					type="submit"
					:disabled="loading"
					:loading="loading"
				>
					{{ loading ? $t('screens.forgotPassword.submitting') : $t('screens.forgotPassword.submit') }}
				</Button>
			</Form>

			<p class="mt-16 text-black opacity-60">
				{{ $t('screens.forgotPassword.remembered') }}
				<Link to="/login">{{ $t('common.backToLogin') }}</Link>
			</p>
		</Card>
	</div>
</template>
