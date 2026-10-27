<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import { storeToRefs } from 'pinia'
import { Button, Input, Card, Alert, Link, Form } from '@trainpaths/nb-ui'
import { useGuestScreen } from '../session'

const router = useRouter()
const authStore = useAuthStore()
const { error, loading } = storeToRefs(authStore)
const { target, carry } = useGuestScreen()

const email = ref('')
const password = ref('')
const displayName = ref('')

// Staff accounts are created by a super admin (authStore.registerStaff), not via self-signup.
async function handleSubmit() {
	const success = await authStore.register(email.value, password.value, displayName.value || undefined)
	if (success) router.push(target())
}
</script>

<template>
	<div class="p-32 max-w-480 mx-auto font-sans">
		<Card>
			<h1 class="text-2xl font-bold mb-24">{{ $t('screens.register.heading') }}</h1>

			<Form @submit="handleSubmit">
				<Input
					v-model="email"
					type="email"
					:placeholder="$t('common.email')"
					required
				/>
				<Input
					v-model="password"
					type="password"
					:placeholder="$t('common.password')"
					required
				/>
				<Input
					v-model="displayName"
					:placeholder="$t('screens.register.displayName')"
				/>
				<Alert
					v-if="error"
					type="error"
				>
					{{ error }}
				</Alert>
				<Button
					type="submit"
					:loading="loading"
				>
					{{ loading ? $t('screens.register.submitting') : $t('screens.register.submit') }}
				</Button>
			</Form>

			<p class="mt-16 text-black opacity-60">
				{{ $t('screens.register.haveAccount') }}
				<Link :to="carry('/login')">{{ $t('screens.register.login') }}</Link>
			</p>
		</Card>
	</div>
</template>
