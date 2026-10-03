// Public-site side: an instance's block *View.vue, src/templates/*.vue and src/overrides/*.vue. Server-rendered and
// shipped to visitors, so nothing here pulls in editor code (no window/document in setup either).
export { default as ViewBlockList } from './lib/web-editor/view/ViewBlockList.vue'
export { default as PageContent } from './views/PageContent.vue'
export { useMediaImage } from './lib/web-editor/media/useMediaImage'
export { useSiteConfigStore } from './stores/siteConfig'
export { usePublicMenusStore } from './stores/menus'
export { default as SiteConfigEntry } from './components/SiteConfigEntry.vue'
export { firmName, configEntry, filledEntries, hasValue, addressLines, entryHref, entryText } from './lib/siteConfig'
export type { TemplatePage, TemplateProps } from './public/templates'
export type {
	BlockInstance,
	ConfigAddress,
	ConfigEntry,
	ConfigFieldType,
	MediaRef,
	PublicMenu,
	PublicMenuItem,
	PublicPage,
	SiteConfig,
} from './lib/web-editor/core/types'
