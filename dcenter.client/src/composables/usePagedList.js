import { ref } from 'vue'
import { errorText } from '@/utils/errors'

export function usePagedList(fetchPage, messages) {
  const items = ref([])
  const total = ref(0)
  const loading = ref(false)
  const loadingMore = ref(false)
  const error = ref('')
  let token = 0

  async function reload() {
    const current = ++token
    loading.value = true
    error.value = ''
    try {
      const page = await fetchPage(0)
      if (current !== token) return
      items.value = page.items
      total.value = page.total
    } catch (e) {
      if (current === token) error.value = errorText(e, messages.load)
    } finally {
      if (current === token) loading.value = false
    }
  }

  async function loadMore() {
    const current = token
    loadingMore.value = true
    try {
      const page = await fetchPage(items.value.length)
      if (current !== token) return
      items.value = [...items.value, ...page.items]
      total.value = page.total
    } catch (e) {
      if (current === token) error.value = errorText(e, messages.more)
    } finally {
      loadingMore.value = false
    }
  }

  return { items, total, loading, loadingMore, error, reload, loadMore }
}
