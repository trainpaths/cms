<script setup lang="ts">
import { computed } from 'vue'
import { themeColors } from '../core/colors'

/**
 * Palette swatches + none; the model is the colour key. `colors` defaults to the whole theme palette
 * (cms.config.json `theme.colors`); blocks pass `paletteFor(supports.…)`.
 */

const props = withDefaults(defineProps<{ colors?: Record<string, string> }>(), { colors: () => themeColors })
const model = defineModel<string>({ default: '' })

const swatches = computed(() => Object.entries(props.colors).map(([key, value]) => ({ key, value })))
</script>

<template>
	<div class="flex flex-wrap gap-6">
		<button
			type="button"
			class="h-24 w-24 cursor-pointer rounded border border-gray-300 bg-white"
			:class="model === '' ? 'ring-2 ring-primary ring-offset-1' : ''"
			:title="$t('editor.colorNone')"
			:aria-label="$t('editor.colorNone')"
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
			{{ $t('editor.noColors') }}
		</p>
	</div>
</template>
