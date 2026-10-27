<script setup lang="ts">
import { useRouter } from 'vue-router'
import { Loading } from '@trainpaths/nb-ui'
import ChangePasswordForm from '../../components/ChangePasswordForm.vue'
import { useCustomerSession } from '../session'

/** Customer password change. The server signs out every session: log in again afterwards. */
const router = useRouter()
const ready = useCustomerSession()
</script>

<template>
	<div class="mx-auto max-w-480 p-32 font-sans">
		<ChangePasswordForm
			v-if="ready"
			back-to="/user/profile"
			@changed="router.push({ name: 'login' })"
		/>
		<Loading
			v-else
			:label="$t('common.loading')"
		/>
	</div>
</template>
