const FORMULA_START = /^[=+\-@\t\r]/

// Same rule as the server export: a cell a spreadsheet would run as a formula gets a leading apostrophe;
// plain numbers (including negative ones) stay as they are.
export function csvCell(value) {
  let text = String(value ?? '')
  if (FORMULA_START.test(text) && !Number.isFinite(Number(text.trim()))) text = `'${text}`
  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text
}

export function downloadCsv(fileName, rows) {
  const content = '﻿' + rows.map((r) => r.map(csvCell).join(',')).join('\r\n')
  const url = URL.createObjectURL(new Blob([content], { type: 'text/csv;charset=utf-8' }))
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  a.click()
  URL.revokeObjectURL(url)
}

export function saveBlob(blob, fileName) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}
