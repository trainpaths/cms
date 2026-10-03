<script setup lang="ts">
import { computed } from 'vue'
import PageSettings from './PageSettings.vue'
import ColorPicker from './ColorPicker.vue'
import SettingsSection from './SettingsSection.vue'
import SidebarTabs from './SidebarTabs.vue'
import { getBlockType } from '../core/blockRegistry'
import { useEditorStore } from '../../../stores/editor'
import { useSelection } from '../composables/useSelection'

const store = useEditorStore()
const { selectedBlock } = useSelection()

const tabs = [
	{ id: 'page', label: 'Page' },
	{ id: 'block', label: 'Block' },
] as const

const blockType = computed(() => {
	if (!selectedBlock.value) return null
	return getBlockType(selectedBlock.value.name)
})

const blockWidth = computed({
	get: () => (selectedBlock.value?.attributes.blockWidth as string) || 'default',
	set: (value: string) => {
		if (selectedBlock.value) {
			store.updateBlockAttributes(selectedBlock.value.id, { blockWidth: value })
		}
	},
})

const backgroundColor = computed({
	get: () => (selectedBlock.value?.attributes.backgroundColor as string) || '',
	set: (value: string) => {
		if (selectedBlock.value) {
			store.updateBlockAttributes(selectedBlock.value.id, { backgroundColor: value })
		}
	},
})

const textColor = computed({
	get: () => (selectedBlock.value?.attributes.textColor as string) || '',
	set: (value: string) => {
		if (selectedBlock.value) {
			store.updateBlockAttributes(selectedBlock.value.id, { textColor: value })
		}
	},
})

const widthOptions = [
	{ value: 'default', label: 'Default' },
	{ value: 'wide', label: 'Wide' },
	{ value: 'full', label: 'Full' },
]
</script>

<template>
	<div class="flex h-full flex-col">
		<SidebarTabs
			v-model="store.rightSidebarTab"
			:tabs="tabs"
			close-testid="sidebar-close-right"
			@close="store.toggleSettingsPanel()"
		/>

		<PageSettings
			v-if="store.rightSidebarTab === 'page'"
			class="scrollbar-thin scrollbar-stable flex-1 overflow-y-auto"
		/>

		<div
			v-else-if="selectedBlock && blockType"
			class="scrollbar-thin scrollbar-stable flex-1 overflow-y-auto p-16"
		>
			<div class="mb-16 flex items-center gap-8 border-b border-gray-200 pb-16">
				<span class="h-20 w-20 text-gray-500">
					<component
						:is="blockType.icon"
						class="h-full w-full"
					/>
				</span>
				<span class="text-sm font-medium text-gray-700">{{ blockType.title }}</span>
			</div>

			<component
				:is="blockType.settings"
				v-if="blockType.settings"
				:block="selectedBlock"
			/>

			<SettingsSection
				v-if="blockType.supports?.width"
				title="Width"
			>
				<div class="flex rounded-md border border-gray-200">
					<button
						v-for="opt in widthOptions"
						:key="opt.value"
						class="flex-1 cursor-pointer border-none px-12 py-6 text-xs font-medium transition-colors"
						:class="
							blockWidth === opt.value
								? 'bg-primary text-white'
								: 'bg-white text-gray-600 hover:bg-gray-50'
						"
						@click="blockWidth = opt.value"
					>
						{{ opt.label }}
					</button>
				</div>
			</SettingsSection>

			<SettingsSection
				v-if="blockType.supports?.backgroundColor"
				title="Background Color"
			>
				<ColorPicker v-model="backgroundColor" />
			</SettingsSection>

			<SettingsSection
				v-if="blockType.supports?.textColor"
				title="Text Color"
			>
				<ColorPicker v-model="textColor" />
			</SettingsSection>

			<p
				v-if="
					!blockType.settings &&
					!blockType.supports?.width &&
					!blockType.supports?.backgroundColor &&
					!blockType.supports?.textColor
				"
				class="text-center text-sm text-gray-400"
			>
				No settings available for this block
			</p>
		</div>

		<p
			v-else
			class="m-0 p-16 text-center text-sm text-gray-400"
		>
			No block selected
		</p>
	</div>
</template>
