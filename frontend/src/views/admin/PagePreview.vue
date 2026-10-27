<script setup lang="ts">
import { computed, ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { Link, Loading, Select } from '@trainpaths/nb-ui'
import { getApiPagesById } from '../../api/sdk.gen'
import type { PageDetail } from '../../lib/web-editor'
import PageView from '../PageView.vue'
import SiteFooter from '../../components/SiteFooter.vue'
import SiteHeader from '../../components/SiteHeader.vue'
import { usePublicMenusStore } from '../../stores/menus'
import { useSiteConfigStore } from '../../stores/siteConfig'
import { DEFAULT_LANGUAGE, IS_LOCALIZED, LANGUAGES, languageLabel, languageOf } from '../../i18n/languages'
import { localizeBlocks, localizeText } from '../../lib/web-editor/core/localize'

const props = defineProps<{ id: string }>()

const page = ref<PageDetail | null>(null)
// the saved working copy in one language, like the public site will show it (`?lang=` from the editor)
const lang = ref(languageOf(useRoute().query.lang).code)
const languageOptions = LANGUAGES.map(({ code }) => ({ value: code, label: languageLabel(code) }))
const localized = computed(() =>
	page.value
		? {
				...localizeText(page.value, page.value.translations, lang.value, DEFAULT_LANGUAGE.code),
				blocks: localizeBlocks(page.value.blocks, lang.value, DEFAULT_LANGUAGE.code),
			}
		: null,
)
const failed = ref(false)
const siteConfig = useSiteConfigStore()
const menus = usePublicMenusStore()
menus.load('main')

onMounted(async () => {
	siteConfig.load()
	try {
		page.value = (await getApiPagesById({ path: { id: props.id } })).data
	} catch {
		failed.value = true
	}
})
</script>

<template>
	<div class="border-b border-yellow-200 bg-yellow-50 px-16 py-8 text-center text-sm text-yellow-800 font-sans">
		{{ page?.status === 'draft' ? $t('admin.preview.draft') : $t('admin.preview.label') }} &middot;
		<Link :to="`/admin/pages/${id}`">{{ $t('admin.preview.back') }}</Link>
		<template v-if="IS_LOCALIZED">
			&middot;
			<Select
				v-model="lang"
				:options="languageOptions"
				size="sm"
				class="inline-flex w-auto align-middle"
				:aria-label="$t('editor.translations.previewLanguage')"
				data-testid="preview-language"
			/>
		</template>
	</div>
	<div
		v-if="localized"
		class="site-theme"
		:lang="languageOf(lang).tag"
	>
		<SiteHeader
			:config="siteConfig.config"
			:menu="menus.menus.main ?? null"
		/>
		<PageView
			:page="localized"
			:config="siteConfig.config"
		/>
		<SiteFooter :config="siteConfig.config" />
	</div>
	<p
		v-else-if="failed"
		class="p-32 text-center text-gray-500"
	>
		{{ $t('admin.preview.notFound') }}
	</p>
	<Loading
		v-else
		:label="$t('admin.preview.loading')"
	/>
</template>
