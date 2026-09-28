import { defineStore } from 'pinia'
import api from '@/utils/api'

let searchToken = 0
let defaultRequest = null

export const useWelderStore = defineStore('welder', {
  state: () => ({
    defaults: [],
    items: [],
  }),

  actions: {
    async loadDefaults() {
      defaultRequest ??= api.get('/welders/search', { params: { q: '' } })
        .then(({ data }) => { this.defaults = data ?? [] })
        .catch(() => { defaultRequest = null })
      await defaultRequest
      return this.defaults
    },

    async search(q) {
      const term = (q ?? '').trim()
      const token = ++searchToken
      if (!term) {
        const list = await this.loadDefaults()
        if (token === searchToken) this.items = list
        return
      }
      try {
        const { data } = await api.get('/welders/search', { params: { q: term } })
        if (token === searchToken) this.items = data ?? []
      } catch {
        // keep the current list
      }
    },
  },
})
