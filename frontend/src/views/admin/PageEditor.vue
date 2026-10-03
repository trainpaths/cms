<script setup lang="ts">
import { useRouter } from 'vue-router'
import { WebEditor } from '../../lib/web-editor'

defineProps<{ id: string }>()

const router = useRouter()
</script>

<template>
	<WebEditor
		:page-id="id"
		public-base=""
		:preview-path="`/admin/pages/${id}/preview`"
		@back="router.push({ name: 'admin-pages' })"
	/>
</template>
