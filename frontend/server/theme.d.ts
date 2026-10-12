/** `theme.colors` of the app's `cms.config.json` ({} without); throws on an invalid key or colour. */
export declare function readThemeColors(root: string): Record<string, string>

/** `theme.colors` from cms.config.json content; throws on an invalid key or colour. */
export declare function themeColors(json: string, source?: string): Record<string, string>

/** `<style>` setting `--color-<key>` on `.site-theme` ('' for no colours). */
export declare function themeStyle(colors: Record<string, string>): string

/** Appends `style` to `<head>`. */
export declare const injectThemeStyle: (html: string, style: string) => string
