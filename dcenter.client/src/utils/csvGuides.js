import { ELECTRODE } from '@/utils/consumables'

const list = (values) => (values ?? []).join(', ')

function compartmentCodes(catalog) {
  const byOven = new Map()
  for (const c of catalog.compartments ?? []) {
    const prefix = c.code.split('-')[0]
    if (!byOven.has(prefix)) byOven.set(prefix, { ovenType: c.ovenType, count: 0 })
    byOven.get(prefix).count += 1
  }
  return [...byOven].map(([prefix, o]) => `${prefix}-1 … ${prefix}-${o.count} (${o.ovenType})`).join(', ')
}

export function stockGuide(catalog) {
  return [
    { column: 'Date', required: 'No', format: 'YYYY-MM-DD or DD/MM/YYYY. Blank = today. Not in the future.', allowed: '', example: '2026-01-15' },
    { column: 'Type', required: 'Yes', format: 'Consumable type', allowed: list(catalog.categories), example: ELECTRODE },
    { column: 'Specification', required: 'Yes', format: 'AWS classification. Saved in UPPERCASE.', allowed: '', example: 'E7018' },
    { column: 'Diameter', required: 'Yes', format: 'mm with 2 decimals, or a mesh size A/B for bare & powder fillers', allowed: 'Number above 0 and below 100, or mesh like 80/325 (bare & powder only)', example: '3.20' },
    { column: 'Brand', required: 'Yes', format: 'Matched to the Brand list ignoring case, otherwise Title Case', allowed: '', example: 'Kobelco' },
    { column: 'Lot / Heat No.', required: 'Yes', format: 'Saved in UPPERCASE, max 60 characters', allowed: '', example: 'L12345' },
    { column: 'Quantity (KG)', required: 'Yes', format: 'kg with a dot for decimals', allowed: 'Above 0, up to 99,999.99', example: '5.00' },
    { column: 'Storage', required: 'No', format: `Blank = ${catalog.stages?.[0] ?? 'Normal'}`, allowed: list(catalog.stages), example: 'Activated' },
    {
      column: 'Compartment', required: 'Electrodes in Activated',
      format: 'Oven code and number. Blank = Unassigned. Leave blank for bare & powder fillers and Normal storage.',
      allowed: compartmentCodes(catalog), example: 'AS-1',
    },
    {
      column: 'Holding Oven', required: 'No', format: 'Only used for a new electrode. Blank = taken from the compartment.',
      allowed: list(catalog.ovenTypes), example: catalog.ovenTypes?.[0] ?? '',
    },
    { column: 'Source', required: 'No', format: `Blank = ${catalog.sources?.[0] ?? ''}`, allowed: list(catalog.sources), example: catalog.sources?.[0] ?? '' },
    { column: 'Remarks', required: 'No', format: 'Free text, max 500 characters. Rows starting with EXAMPLE are skipped.', allowed: '', example: 'Opening balance' },
  ]
}

export function itemGuide(catalog) {
  return [
    { column: 'Type', required: 'Yes', format: 'Consumable type', allowed: list(catalog.categories), example: ELECTRODE },
    { column: 'Specification', required: 'Yes', format: 'AWS classification. Saved in UPPERCASE.', allowed: '', example: 'E7018' },
    { column: 'Diameter', required: 'Yes', format: 'mm with 2 decimals, or a mesh size A/B for bare & powder fillers', allowed: 'Number above 0 and below 100, or mesh like 80/325 (bare & powder only)', example: '3.20' },
    { column: 'Min Stock (KG)', required: 'No', format: 'kg. Blank = 0 for new rows, unchanged for existing ones.', allowed: '0 or more', example: '20' },
    { column: 'Activated Min (KG)', required: 'No', format: 'kg. Blank = 0 for new rows, unchanged for existing ones.', allowed: '0 or more', example: '5' },
    { column: 'Finish Threshold (KG)', required: 'No', format: 'kg. Blank = use the default threshold.', allowed: '0 or more', example: '' },
    { column: 'Holding Oven Type', required: 'Electrodes', format: 'Oven the electrode is held in', allowed: list(catalog.ovenTypes), example: catalog.ovenTypes?.[0] ?? '' },
    { column: 'Active', required: 'No', format: 'Blank = Yes', allowed: 'Yes, No', example: 'Yes' },
  ]
}
