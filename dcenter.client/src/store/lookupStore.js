import { defineStore } from 'pinia';
import api from '@/utils/api';

const CATEGORIES = ['Process', 'Size', 'Type', 'Manuf'];

export const useLookupStore = defineStore('lookup', {
  state: () => ({
    all: [],
    loaded: false,
  }),

  getters: {
    options: (s) => {
      const map = Object.fromEntries(CATEGORIES.map((c) => [c, []]));
      for (const l of s.all) {
        if (l.isActive && map[l.category]) map[l.category].push(l.value);
      }
      return map;
    },
  },

  actions: {
    async load(force = false) {
      if (this.loaded && !force) return;
      const { data } = await api.get('/lookups');
      this.all = data;
      this.loaded = true;
    },
  },
});
