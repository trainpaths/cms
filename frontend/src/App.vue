<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { ConfirmDialog, ToastContainer } from '@trainpaths/nb-ui'
import AppNav from './components/AppNav.vue'

const route = useRoute()
// signed-in app views only; editor + preview are full-screen
const showNav = computed(() => !!route.meta.requiresAuth && !route.meta.hideNav)
</script>

<template>
	<AppNav v-if="showNav" />
	<!-- md+: room for the fixed sidebar -->
	<div :class="{ 'md:pl-220': showNav }">
		<router-view />
	</div>
	<ToastContainer />
	<ConfirmDialog />
</template>
