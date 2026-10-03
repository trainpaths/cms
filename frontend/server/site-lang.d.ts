export declare const DEFAULT_LANG: string

/** `site.lang` of the app's `cms.config.json` (default `en`); throws on an invalid tag. */
export declare function readSiteLang(root: string): string

/** `site.lang` from cms.config.json content (comments + trailing commas allowed); throws on an invalid tag. */
export declare function siteLang(json: string, source?: string): string

/** Sets `lang` on the `<html>` tag, replacing the template's own. */
export declare const setHtmlLang: (html: string, lang: string) => string
