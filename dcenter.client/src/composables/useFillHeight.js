import { onBeforeUnmount, onMounted, ref } from 'vue'

export function useFillHeight(target, minHeight = 160) {
  const height = ref(minHeight)
  let observer = null

  onMounted(() => {
    observer = new ResizeObserver(([entry]) => {
      height.value = Math.max(minHeight, Math.floor(entry.contentRect.height))
    })
    if (target.value) observer.observe(target.value)
  })

  onBeforeUnmount(() => observer?.disconnect())

  return height
}
