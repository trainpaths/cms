<script setup lang="ts">
import { computed } from 'vue'
import type { BlockInstance } from '../../core/types'
import { useLanguageSwitch } from '../../../../i18n'
import { DEFAULT_LANGUAGE } from '../../../../i18n/languages'
import { localeHref } from '../../../../public/locale'

const props = defineProps<{ block: BlockInstance }>()

const label = computed(() => (props.block.attributes.label as string) || '')
const languageSwitch = useLanguageSwitch()
const url = computed(() => (props.block.attributes.url as string) || '#')
// one URL for every language: site paths follow the page's language
const href = computed(() => localeHref(url.value, languageSwitch?.current.value.code ?? DEFAULT_LANGUAGE.code))
const colorClass = computed(() => (props.block.attributes.textColor ? '' : 'text-primary'))
</script>

<template>
	<a
		:href="href"
		class="inline-block p-16 hover:underline"
		:class="colorClass"
		target="_blank"
		rel="noopener noreferrer"
	>
		{{ label || url }}
	</a>
</template>
