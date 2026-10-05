<script setup lang="ts">
import { watch } from 'vue'
// direct imports, not the barrel: this is in the public bundle, the barrel drags in the editor
import ViewBlockList from '../lib/web-editor/view/ViewBlockList.vue'
import { useMediaStore } from '../stores/media'
import type { BlockInstance, MediaRef } from '../lib/web-editor'

const props = defineProps<{ title: string; blocks: BlockInstance[]; media: MediaRef[] }>()

// image blocks resolve their mediaId from the store
const mediaStore = useMediaStore()
watch(
	() => props.media,
	(media) => mediaStore.seed(media),
	{ immediate: true },
)
</script>

<template>
	<!-- @container: blocks use container-query variants (@md:), same classes as in the editor canvas.
		w-full: containment drops content-based width, and mx-auto in a flex column would collapse it to 0 -->
	<article class="@container mx-auto w-full max-w-3xl px-24 py-48 font-sans">
		<!-- sr-only not hidden: visually gone, still the page h1 for a11y/SEO -->
		<h1 class="sr-only">{{ title }}</h1>
		<!-- templates (src/templates/) add content around the owner's blocks -->
		<slot name="before" />
		<div class="flex flex-col gap-10">
			<ViewBlockList :blocks="blocks" />
		</div>
		<slot name="after" />
	</article>
</template>
