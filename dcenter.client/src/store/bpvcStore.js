import { defineStore } from 'pinia'
import api from '@/utils/api'

function norm(v) {
  return (v ?? '').trim().replace(/\s+/g, ' ').toLowerCase()
}

function gradeOf(b) {
  return b.designation?.trim() || b.unsNo?.trim() || ''
}

export const useBpvcStore = defineStore('bpvc', {
  state: () => ({
    items: [],
    loaded: false,
    loading: false,
  }),

  getters: {
    materialOptions: (s) =>
      [...new Set(s.items.map((b) => b.specNo).filter(Boolean))].sort(),
    gradeOptions: (s) => [...new Set(s.items.map(gradeOf).filter(Boolean))].sort(),
    pNoIndex: (s) => {
      const index = new Map()
      for (const b of s.items) {
        const m = norm(b.specNo)
        const g = norm(gradeOf(b))
        if (!m || !g || !b.pNo) continue
        const key = `${m}|${g}`
        const list = index.get(key) ?? []
        if (!list.includes(b.pNo)) list.push(b.pNo)
        index.set(key, list)
      }
      return index
    },
    gradesByMaterial: (s) => {
      const bySpec = new Map()
      for (const b of s.items) {
        const m = norm(b.specNo)
        const g = gradeOf(b)
        if (!m || !g) continue
        const set = bySpec.get(m) ?? new Set()
        set.add(g)
        bySpec.set(m, set)
      }
      return new Map([...bySpec].map(([k, set]) => [k, [...set].sort()]))
    },
  },

  actions: {
    async load(force = false) {
      if ((this.loaded || this.loading) && !force) return
      this.loading = true
      try {
        const { data } = await api.get('/bpvc')
        this.items = data
        this.loaded = true
      } catch {
        this.items = []
      } finally {
        this.loading = false
      }
    },

    gradesFor(specNo) {
      return this.gradesByMaterial.get(norm(specNo)) ?? this.gradeOptions
    },

    resolvePNo(specNo, grade) {
      const m = norm(specNo)
      const g = norm(grade)
      if (!m || !g) return ''
      return this.pNoIndex.get(`${m}|${g}`)?.[0] ?? ''
    },
  },
})
