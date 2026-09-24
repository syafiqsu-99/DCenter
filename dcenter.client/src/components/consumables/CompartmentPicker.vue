<template>
  <div>
    <v-alert v-if="!ovenType" type="warning" variant="tonal" density="compact">
      This consumable has no holding oven type. Ask a supervisor to set it under Consumables.
    </v-alert>
    <v-skeleton-loader v-else-if="store.loadingOvens && !oven" type="image" />
    <template v-else-if="oven">
      <div class="text-caption text-medium-emphasis mb-1">{{ oven.name }} — tap a compartment</div>
      <OvenLayout :oven="oven" mode="pick" :item-id="itemId" :selected-id="modelValue" :exclude-id="excludeId"
                  @select="emit('update:modelValue', $event.id)" />
    </template>
  </div>
</template>

<script setup>
  import { computed, onMounted } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import OvenLayout from '@/components/consumables/OvenLayout.vue'

  const props = defineProps({
    modelValue: { type: Number, default: null },
    ovenType: { type: String, default: null },
    itemId: { type: Number, default: null },
    excludeId: { type: Number, default: null },
  })
  const emit = defineEmits(['update:modelValue'])

  const store = useConsumableStore()
  const oven = computed(() => store.ovenBoard.ovens.find((o) => o.ovenType === props.ovenType) ?? null)

  onMounted(() => store.loadOvens())
</script>
