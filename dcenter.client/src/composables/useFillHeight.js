import { nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { useDisplay } from 'vuetify'

export function useFillHeight(target, { bottomGap = 24, min = 420 } = {}) {
  const { lgAndUp } = useDisplay()
  const height = ref('auto')

  function update() {
    const el = target.value?.$el ?? target.value
    if (!el || !lgAndUp.value) {
      height.value = 'auto'
      return
    }
    const top = el.getBoundingClientRect().top + window.scrollY
    const footer = document.querySelector('footer, .v-footer')?.offsetHeight ?? 0
    height.value = `${Math.max(min, window.innerHeight - top - footer - bottomGap)}px`
  }

  onMounted(async () => {
    await nextTick()
    update()
    window.addEventListener('resize', update)
  })
  onBeforeUnmount(() => window.removeEventListener('resize', update))

  return { height, update }
}
