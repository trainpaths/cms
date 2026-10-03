<script setup lang="ts">
const model = defineModel<string>({ default: '' })

const presetColors = [
	'', // transparent/none
	'#ffffff',
	'#f3f4f6',
	'#e5e7eb',
	'#1f2937',
	'#111827',
	'#ef4444',
	'#f97316',
	'#eab308',
	'#22c55e',
	'#3b82f6',
	'#8b5cf6',
	'#ec4899',
]

function selectColor(color: string) {
	model.value = color
}

function clearColor() {
	model.value = ''
}
</script>

<template>
	<div class="flex flex-col gap-8">
		<div class="flex flex-wrap gap-6">
			<button
				v-for="color in presetColors"
				:key="color || 'none'"
				class="h-24 w-24 cursor-pointer rounded border"
				:class="[
					model === color ? 'ring-2 ring-primary ring-offset-1' : '',
					color === '' ? 'border-gray-300 bg-white' : 'border-transparent',
				]"
				:style="color ? { backgroundColor: color } : {}"
				:title="color || 'None'"
				@click="selectColor(color)"
			>
				<span
					v-if="!color"
					class="text-xs text-gray-400"
					>&#8709;</span
				>
			</button>
		</div>
		<div class="flex items-center gap-8">
			<input
				type="color"
				:value="model || '#000000'"
				class="h-28 w-40 cursor-pointer rounded border border-gray-300"
				@input="model = ($event.target as HTMLInputElement).value"
			/>
			<input
				type="text"
				:value="model"
				placeholder="Custom..."
				class="h-28 flex-1 rounded border border-gray-300 px-8 text-xs"
				@input="model = ($event.target as HTMLInputElement).value"
			/>
			<button
				v-if="model"
				class="h-28 cursor-pointer rounded border border-gray-300 bg-white px-8 text-xs text-gray-500 hover:bg-gray-50"
				@click="clearColor"
			>
				Clear
			</button>
		</div>
	</div>
</template>
