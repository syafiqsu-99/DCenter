import { onBeforeUnmount, ref, watch } from 'vue'

export function useFillHeight(target, minHeight = 160) {
  const height = ref(minHeight)
  const observer = new ResizeObserver(([entry]) => {
    height.value = Math.max(minHeight, Math.floor(entry.contentRect.height))
  })

  const stop = watch(target, (el, old) => {
    if (old) observer.unobserve(old)
    if (el) observer.observe(el)
  }, { immediate: true, flush: 'post' })

  onBeforeUnmount(() => {
    stop()
    observer.disconnect()
  })

  return height
}
