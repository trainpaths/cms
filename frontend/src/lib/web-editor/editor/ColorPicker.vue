<script setup lang="ts">
import { themeColors } from '../core/colors'

/** Theme palette swatches (cms.config.json `theme.colors`) + none; the model is the colour key. */

const model = defineModel<string>({ default: '' })

const swatches = Object.entries(themeColors).map(([key, value]) => ({ key, value }))
</script>

<template>
	<div class="flex flex-wrap gap-6">
		<button
			type="button"
			class="h-24 w-24 cursor-pointer rounded border border-gray-300 bg-white"
			:class="model === '' ? 'ring-2 ring-primary ring-offset-1' : ''"
			title="None"
			aria-label="None"
			:aria-pressed="model === ''"
			@click="model = ''"
		>
			<span class="text-xs text-gray-400">&#8709;</span>
		</button>
		<button
			v-for="swatch in swatches"
			:key="swatch.key"
			type="button"
			class="h-24 w-24 cursor-pointer rounded border border-gray-300"
			:class="model === swatch.key ? 'ring-2 ring-primary ring-offset-1' : ''"
			:style="{ backgroundColor: swatch.value }"
			:title="swatch.key"
			:aria-label="swatch.key"
			:aria-pressed="model === swatch.key"
			data-testid="color-swatch"
			@click="model = swatch.key"
		></button>
		<p
			v-if="!swatches.length"
			class="m-0 w-full text-xs text-gray-400"
		>
			No theme colours set up for this site.
		</p>
	</div>
</template>
