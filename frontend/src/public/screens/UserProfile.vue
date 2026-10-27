<script setup lang="ts">
import { useRouter } from 'vue-router'
import { Loading } from '@trainpaths/nb-ui'
import ProfileDetails from '../../components/ProfileDetails.vue'
import { useCustomerSession } from '../session'

/** Customer account page. Server-rendered as a shell; the details load once the session is confirmed. */
const router = useRouter()
const ready = useCustomerSession()
</script>

<template>
	<div class="mx-auto max-w-600 p-32 font-sans">
		<h1 class="mb-24 text-2xl font-bold">{{ $t('screens.profile.title') }}</h1>
		<ProfileDetails
			v-if="ready"
			password-to="/user/password"
			@logged-out="router.push('/')"
		/>
		<Loading
			v-else
			:label="$t('common.loading')"
		/>
	</div>
</template>
