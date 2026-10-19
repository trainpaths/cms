<script setup lang="ts">
import { useRouter } from 'vue-router'
import ChangePasswordForm from '../components/ChangePasswordForm.vue'

const router = useRouter()
</script>

<template>
	<div class="mx-auto max-w-480 p-32 font-sans">
		<ChangePasswordForm
			back-to="/admin/profile"
			@changed="router.push({ name: 'admin-login' })"
		/>
	</div>
</template>
