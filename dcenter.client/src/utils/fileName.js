function shortDesc(desc) {
  return (desc ?? '').trim().split(/\s+/).slice(0, 6).join(' ').slice(0, 40)
}

export function reportFileName(workOrderNumber, partNo, description) {
  return [workOrderNumber, partNo, shortDesc(description)]
    .map((p) => (p ?? '').toString().trim())
    .filter(Boolean)
    .join(' ')
    .replace(/[\\/:*?"<>|]+/g, '')
    .replace(/\s+/g, ' ')
    .trim() || 'WeldOrderCard'
}
