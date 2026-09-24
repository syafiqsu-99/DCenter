import { BarElement, CategoryScale, Chart, Legend, LinearScale, Tooltip } from 'chart.js'

Chart.register(BarElement, CategoryScale, LinearScale, Legend, Tooltip)

export const TYPE_COLORS = ['#1565C0', '#EF6C00', '#2E7D32', '#6A1B9A']
export const IN_COLOR = '#2E7D32'
export const OUT_COLOR = '#1565C0'
export const BALANCE_COLOR = '#1565C0'
export const LOW_COLOR = '#EF6C00'
export const NORMAL_COLOR = '#78909C'
export const ACTIVATED_COLOR = '#1565C0'
export const BAKING_COLOR = '#EF6C00'

export const kgTooltip = {
  callbacks: {
    label: (ctx) => `${ctx.dataset.label ?? ctx.label}: ${Number(ctx.parsed.y ?? ctx.parsed).toFixed(2)} kg`,
  },
}
