<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import { storeToRefs } from 'pinia'
import { Button, Input, Card, Alert, Link, Form } from '@trainpaths/nb-ui'
import { useGuestScreen } from '../session'

// Customer login (public site). Staff sign in at /admin/login (admin SPA, AdminLogin.vue).
const router = useRouter()
const authStore = useAuthStore()
const { error, loading } = storeToRefs(authStore)
const { target, carry } = useGuestScreen()

const email = ref('')
const password = ref('')

authStore.clearError()

async function handleSubmit() {
	const success = await authStore.login('customer', email.value, password.value)
	if (success) router.push(target())
}
</script>

<template>
	<div class="p-32 max-w-480 mx-auto font-sans">
		<Card>
			<h1 class="text-2xl font-bold mb-24">{{ $t('screens.login.heading') }}</h1>

			<Form @submit="handleSubmit">
				<Input
					v-model="email"
					type="email"
					:placeholder="$t('common.email')"
				/>
				<Input
					v-model="password"
					type="password"
					:placeholder="$t('common.password')"
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
					{{ loading ? $t('screens.login.submitting') : $t('screens.login.submit') }}
				</Button>
			</Form>

			<p class="mt-16 text-black opacity-60">
				{{ $t('screens.login.noAccount') }}
				<Link :to="carry('/register')">{{ $t('screens.login.register') }}</Link>
			</p>
			<p class="mt-8 text-black opacity-60">
				<Link to="/forgot-password">{{ $t('screens.login.forgot') }}</Link>
			</p>
		</Card>
	</div>
</template>
