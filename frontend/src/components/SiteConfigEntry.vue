<script setup lang="ts">
import type { ConfigEntry } from '../lib/web-editor'
import { addressLines, entryHref, entryText } from '../lib/siteConfig'

/**
 * One site config entry's value as the public site shows it: an address as lines, email/phone/link as a link
 * (mailto:/tel:/the URL, links in a new tab), text as entered. Footer, templates and overrides; attrs land on the root.
 */
defineProps<{ entry: ConfigEntry }>()
</script>

<template>
	<address
		v-if="entry.type === 'address'"
		class="not-italic"
	>
		<span
			v-for="line in addressLines(entry.address)"
			:key="line"
			class="block"
			>{{ line }}</span
		>
	</address>
	<a
		v-else-if="entryHref(entry)"
		:href="entryHref(entry)!"
		class="break-words text-primary no-underline hover:underline"
		v-bind="entry.type === 'link' ? { target: '_blank', rel: 'noopener noreferrer' } : {}"
	>
		{{ entryText(entry) }}
	</a>
	<span
		v-else
		class="whitespace-pre-line break-words"
		>{{ entry.value }}</span
	>
</template>
