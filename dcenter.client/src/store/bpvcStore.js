import { defineStore } from 'pinia'
import api from '@/utils/api'

function norm(v) {
  return (v ?? '').trim().replace(/\s+/g, ' ').toLowerCase()
}

function specOf(b) {
  return b.specNoRaw || b.specNo
}

export const useBpvcStore = defineStore('bpvc', {
  state: () => ({
    items: [],
    loaded: false,
    loading: false,
  }),

  getters: {
    materialOptions: (s) =>
      [...new Set(s.items.map(specOf).filter(Boolean))].sort(),
    gradeOptions: (s) => [...new Set(s.items.map((b) => b.designation).filter(Boolean))].sort(),
    pNoIndex: (s) => {
      const index = new Map()
      for (const b of s.items) {
        const m = norm(specOf(b))
        const g = norm(b.designation)
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
        const m = norm(specOf(b))
        if (!m || !b.designation) continue
        const set = bySpec.get(m) ?? new Set()
        set.add(b.designation)
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

    gradesFor(specNoRaw) {
      return this.gradesByMaterial.get(norm(specNoRaw)) ?? this.gradeOptions
    },

    resolvePNo(specNoRaw, designation) {
      const m = norm(specNoRaw)
      const g = norm(designation)
      if (!m || !g) return ''
      return this.pNoIndex.get(`${m}|${g}`)?.[0] ?? ''
    },
  },
})
