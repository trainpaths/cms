<script setup lang="ts">
import { MediaActions, type MediaRef } from '../lib/web-editor'

/** Logo / icon slot on the configuration page: preview, Upload / Choose existing (media library), Remove. */
defineProps<{ title: string; hint: string; testId: string }>()
const model = defineModel<MediaRef | null>({ required: true })
const emit = defineEmits<{ error: [message: string] }>()
</script>

<template>
	<section
		class="flex flex-col gap-12 rounded-lg border border-gray-200 bg-white p-16 shadow-xs"
		:data-testid="testId"
	>
		<div>
			<h2 class="m-0 text-base font-semibold text-gray-900">{{ title }}</h2>
			<p class="m-0 mt-2 text-xs text-gray-500">{{ hint }}</p>
		</div>
		<div
			class="flex h-120 items-center justify-center rounded border border-dashed border-gray-300 bg-gray-50 p-12"
		>
			<img
				v-if="model"
				:src="model.url"
				:alt="model.alt"
				class="max-h-full max-w-full object-contain"
				:data-testid="`${testId}-preview`"
			/>
			<span
				v-else
				class="text-xs text-gray-400"
				>None selected</span
			>
		</div>
		<div class="flex items-center justify-between gap-8">
			<MediaActions
				:replace="!!model"
				:on-error="(message: string) => emit('error', message)"
				@select="model = { id: $event.id, url: $event.url, alt: $event.alt }"
			/>
			<button
				v-if="model"
				type="button"
				class="cursor-pointer border-none bg-transparent p-0 text-xs text-red-600 hover:underline"
				:data-testid="`${testId}-remove`"
				@click="model = null"
			>
				Remove
			</button>
		</div>
	</section>
</template>
