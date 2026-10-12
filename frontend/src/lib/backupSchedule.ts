import type { BackupInterval, BackupSettingsResponse } from '../api/types.gen'

/** Backup schedule labels, shared by the backups panel (form) and the dashboard summary. */

export const backupIntervals: { value: BackupInterval; label: string }[] = [
	{ value: 'off', label: 'Off' },
	{ value: 'daily', label: 'Daily' },
	{ value: 'weekly', label: 'Weekly' },
	{ value: 'biweekly', label: 'Every 2 weeks' },
	{ value: 'monthly', label: 'Monthly' },
]

export const weekdayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']

/** "Weekly on Monday, 03:00 UTC"; "Off" when no automatic backups run. */
export function scheduleSummary(s: Pick<BackupSettingsResponse, 'interval' | 'timeOfDay' | 'weekday' | 'dayOfMonth'>) {
	const label = backupIntervals.find((i) => i.value === s.interval)?.label ?? s.interval
	if (s.interval === 'off') return label
	const day =
		s.interval === 'weekly' || s.interval === 'biweekly'
			? ` on ${weekdayNames[s.weekday]}`
			: s.interval === 'monthly'
				? ` on day ${s.dayOfMonth}`
				: ''
	return `${label}${day}, ${s.timeOfDay} UTC`
}
