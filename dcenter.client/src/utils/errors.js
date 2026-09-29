export function errorText(e, fallback) {
  const data = e?.response?.data
  if (typeof data === 'string' && data.trim()) return data
  if (data?.errors) return Object.values(data.errors).flat().join(' ')
  if (data?.title) return data.title
  return fallback
}
