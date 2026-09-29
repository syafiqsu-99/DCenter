export const pad = (n) => String(n).padStart(2, '0')

function isoDate(d) {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

export function todayIso() {
  return isoDate(new Date())
}

export function daysAgoIso(days) {
  const d = new Date()
  d.setDate(d.getDate() - days)
  return isoDate(d)
}
