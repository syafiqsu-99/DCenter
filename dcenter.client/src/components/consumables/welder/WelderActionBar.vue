<template>
  <div class="action-bar">
    <v-btn v-for="a in actions" :key="a.value" :color="modelValue === a.value ? a.color : undefined"
           :variant="modelValue === a.value ? 'flat' : 'outlined'" height="104" class="action-btn text-none" :disabled="disabled"
           @click="emit('update:modelValue', a.value)">
      <div class="d-flex flex-column align-center">
        <v-icon size="36">{{ a.icon }}</v-icon>
        <span class="text-h6 font-weight-bold mt-1">{{ a.label }}</span>
        <span class="text-caption">{{ a.hint }}</span>
      </div>
    </v-btn>
  </div>
</template>

<script setup>
  defineProps({
    modelValue: { type: String, default: 'use' },
    disabled: { type: Boolean, default: false },
  })
  const emit = defineEmits(['update:modelValue'])

  const actions = [
    { value: 'use', label: 'Use', hint: 'Take consumables', icon: 'mdi-tray-arrow-up', color: 'info' },
    { value: 'return', label: 'Return', hint: 'Give back excess', icon: 'mdi-keyboard-return', color: 'teal' },
    { value: 'baking', label: 'Baking', hint: 'Bake electrodes', icon: 'mdi-fire', color: 'deep-orange' },
    { value: 'holding', label: 'Holding', hint: 'Holding ovens', icon: 'mdi-view-grid-outline', color: 'indigo' },
  ]
</script>

<style scoped>
  .action-bar {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
    gap: 12px;
  }
</style>
