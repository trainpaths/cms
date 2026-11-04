<script setup lang="ts">
import { ConfirmDialog, ToastContainer } from '@trainpaths/nb-ui'
import DemoMenu from './DemoMenu.vue'
</script>

<template>
	<RouterView />
	<DemoMenu />
	<ToastContainer position="right-16 bottom-[25vh]" />
	<ConfirmDialog />
</template>
