<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Button, Icon, Popover, useConfirm } from '@trainpaths/nb-ui'
import { translate } from '@trainpaths/cms/src/i18n/index.ts'
import { useDemo } from './context'

/** Floating round "Demo" button over every screen; opens the demo settings (edit/view, reset, links). */
const route = useRoute()
const router = useRouter()
const { confirm } = useConfirm()
const { reset } = useDemo()

const editing = computed(() => route.name === 'edit')

function go(path: string, close: () => void) {
	close()
	void router.push(path)
}

async function onReset(close: () => void) {
	close()
	const ok = await confirm({
		title: translate('demo.resetTitle'),
		message: translate('demo.resetMessage'),
		confirmText: translate('demo.reset'),
		cancelText: translate('common.cancel'),
		danger: true,
	})
	if (ok) reset()
}

const linkClass = 'text-sm text-primary underline-offset-2 hover:underline'
</script>

<template>
	<!-- z-45: over the editor header (z-40) and sidebars (z-30), under dialogs (z-50: the reset confirm covers it) -->
	<div class="fixed bottom-16 left-16 z-[45] font-sans">
		<Popover
			placement="top"
			align="start"
			:label="$t('demo.title')"
		>
			<template #trigger="{ props: triggerProps, open }">
				<button
					v-bind="triggerProps"
					type="button"
					class="flex size-64 cursor-pointer flex-col items-center justify-center gap-2 rounded-full border-none text-white shadow-lg ring-white/70 transition hover:scale-105 hover:bg-primary focus-visible:ring-4 focus-visible:outline-hidden motion-reduce:transition-none motion-reduce:hover:scale-100"
					:class="open ? 'bg-primary' : 'bg-black'"
					data-testid="demo-menu"
				>
					<Icon
						name="settings"
						:size="22"
					/>
					<span class="text-[10px] leading-none font-semibold tracking-wide uppercase">{{ $t('demo.button') }}</span>
				</button>
			</template>
			<template #default="{ close }">
				<div
					class="flex w-[min(288px,calc(100vw-32px))] flex-col gap-12"
					data-testid="demo-panel"
				>
					<div>
						<p class="m-0 text-base font-semibold text-gray-900">{{ $t('demo.title') }}</p>
						<p class="m-0 mt-4 text-sm text-gray-600">{{ $t('demo.note') }}</p>
					</div>
					<div class="flex flex-col gap-8">
						<Button
							bg="primary"
							block
							data-testid="demo-switch"
							@click="go(editing ? '/home' : '/', close)"
						>
							<Icon
								:name="editing ? 'eye' : 'edit'"
								:size="16"
							/>
							{{ editing ? $t('demo.view') : $t('demo.edit') }}
						</Button>
						<Button
							variant="outline"
							text="danger"
							border="danger"
							block
							data-testid="demo-reset"
							@click="onReset(close)"
						>
							{{ $t('demo.reset') }}
						</Button>
					</div>
					<div class="flex flex-wrap gap-x-16 gap-y-4 border-t border-gray-200 pt-12">
						<a
							href="https://github.com/trainpaths/cms"
							target="_blank"
							rel="noopener"
							:class="linkClass"
							>{{ $t('demo.repo') }}</a
						>
						<a
							href="https://github.com/trainpaths/cms-starter"
							target="_blank"
							rel="noopener"
							:class="linkClass"
							>{{ $t('demo.starter') }}</a
						>
					</div>
				</div>
			</template>
		</Popover>
	</div>
</template>
