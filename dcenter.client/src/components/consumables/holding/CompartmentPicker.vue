<template>
  <div>
    <v-alert v-if="!ovenType" type="warning" variant="tonal" density="compact">
      This consumable has no holding oven type. Ask a supervisor to set it under Consumables.
    </v-alert>
    <v-skeleton-loader v-else-if="store.loadingOvens && !oven" type="image" />
    <v-alert v-else-if="!oven" type="error" variant="tonal" density="compact">
      {{ loadError || `No ${ovenType} oven was found.` }}
      <template #append><v-btn variant="text" size="small" @click="load">Retry</v-btn></template>
    </v-alert>
    <template v-else-if="oven">
      <div class="text-caption text-medium-emphasis mb-1">{{ oven.name }} — tap a compartment</div>
      <OvenLayout :oven="oven" mode="pick" :item-id="itemId" :selected-id="modelValue" :exclude-id="excludeId"
                  @select="emit('update:modelValue', $event.id)" />
    </template>
  </div>
</template>

<script setup>
  import { computed, onMounted, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { errorText } from '@/utils/consumables'
  import OvenLayout from '@/components/consumables/holding/OvenLayout.vue'

  const props = defineProps({
    modelValue: { type: Number, default: null },
    ovenType: { type: String, default: null },
    itemId: { type: Number, default: null },
    excludeId: { type: Number, default: null },
  })
  const emit = defineEmits(['update:modelValue'])

  const store = useConsumableStore()
  const oven = computed(() => store.ovenBoard.ovens.find((o) => o.ovenType === props.ovenType) ?? null)

  const loadError = ref('')

  async function load() {
    loadError.value = ''
    try {
      await store.loadOvens(true)
    } catch (e) {
      loadError.value = errorText(e, 'Could not load the holding ovens.')
    }
  }

  onMounted(() => {
    if (!store.ovenBoardLoaded) load()
  })
</script>
