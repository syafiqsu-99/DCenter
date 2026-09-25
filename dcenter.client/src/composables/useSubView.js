import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'

export function useSubView(options, fallback = options[0]) {
  const route = useRoute()
  const router = useRouter()

  return computed({
    get: () => (options.includes(route.query.view) ? route.query.view : fallback),
    set: (view) => {
      if (view === route.query.view) return
      router.replace({ query: { ...route.query, view: view === fallback ? undefined : view } })
    },
  })
}
