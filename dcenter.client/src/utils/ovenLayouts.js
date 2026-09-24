const STANDARD = {
  columns: '65fr 76fr 77fr 66fr',
  areas: ['c1 c1 c2 c2', 'c3 c4 c5 c6', 'c7 c8 c8 c9'],
}

const NI_ALLOY = {
  columns: '67fr 80fr 68fr 69fr',
  areas: ['c1 c1 c2 c2', 'c3 c4 c4 c5', 'c6 c7 c8 c9'],
}

const OVEN_LAYOUTS = {
  'Alloy Steel': STANDARD,
  'Mild Steel': STANDARD,
  'Ni Alloy': NI_ALLOY,
  'Stainless Steel': STANDARD,
}

function layoutFor(ovenType) {
  return OVEN_LAYOUTS[ovenType] ?? STANDARD
}

export function gridStyle(ovenType) {
  const layout = layoutFor(ovenType)
  return {
    gridTemplateColumns: layout.columns,
    gridTemplateAreas: layout.areas.map((row) => `"${row}"`).join(' '),
  }
}

export function lotSummary(contents) {
  if (!contents.length) return ''
  const [first, ...rest] = contents
  return rest.length ? `Lot ${first.lotNumber} +${rest.length}` : `Lot ${first.lotNumber}`
}
