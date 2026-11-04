<script setup lang="ts">
import { useRouter } from 'vue-router'
import Editor from '@trainpaths/cms/src/lib/web-editor/editor/Editor.vue'
import { DEMO_PAGE_ID } from './seed'

// the real editor; its view link and back button lead to the view (`#/home`)
const router = useRouter()
</script>

<template>
	<Editor
		:page-id="DEMO_PAGE_ID"
		public-base=""
		preview-path="/home"
		@back="router.push('/home')"
	/>
</template>
