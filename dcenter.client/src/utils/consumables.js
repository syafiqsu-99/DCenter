export const COLUMN = {
  requestor: 'Requestor',
  date: 'Date',
  source: 'Source',
  brand: 'Electrode Brand',
  diameter: 'Electrode Diameter (mm)',
  spec: 'Electrode Specification',
  lot: 'Lot\\ heat Number',
  diaSpec: 'Dia & Spec',
  receiveQty: 'Receive Qty (KG)',
  take: 'Take/KG',
  balance: 'BALANCE',
  type: 'Consumable Type',
  minStock: 'Min Stock (KG)',
}

export const ALL = 'All'

export const TXN_COLORS = {
  Receive: 'success',
  Issue: 'info',
  Void: 'error',
}

const kgFormat = new Intl.NumberFormat('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

export function kg(value) {
  return kgFormat.format(Number(value ?? 0))
}

export function todayIso() {
  const d = new Date()
  const pad = (n) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

export function monthStartIso() {
  return `${todayIso().slice(0, 8)}01`
}

export function fmtDate(iso) {
  if (!iso) return ''
  const [y, m, d] = iso.slice(0, 10).split('-')
  return `${Number(d)}/${Number(m)}/${y}`
}

export function fmtDateTime(iso) {
  return iso ? new Date(iso).toLocaleString('en-GB', { dateStyle: 'short', timeStyle: 'short' }) : ''
}

export function fmtTime(iso) {
  return iso ? new Date(iso).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' }) : ''
}

export function errorText(e, fallback) {
  const data = e?.response?.data
  if (typeof data === 'string' && data.trim()) return data
  if (data?.errors) return Object.values(data.errors).flat().join(' ')
  if (data?.title) return data.title
  return fallback
}

export function debounce(fn, ms = 250) {
  let timer = null
  return (...args) => {
    clearTimeout(timer)
    timer = setTimeout(() => fn(...args), ms)
  }
}

export function csvCell(value) {
  const text = String(value ?? '')
  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text
}

export function downloadCsv(fileName, rows) {
  const content = '\uFEFF' + rows.map((r) => r.map(csvCell).join(',')).join('\r\n')
  const url = URL.createObjectURL(new Blob([content], { type: 'text/csv;charset=utf-8' }))
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  a.click()
  URL.revokeObjectURL(url)
}
