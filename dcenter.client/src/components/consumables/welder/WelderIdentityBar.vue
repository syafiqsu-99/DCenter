<template>
  <v-card border flat :color="store.counterWelder ? 'blue-grey-lighten-5' : 'amber-lighten-5'">
    <v-card-text class="d-flex flex-wrap align-center ga-4">
      <v-avatar size="56" :color="store.counterWelder ? 'primary' : 'amber-darken-2'" variant="tonal">
        <v-icon size="32">mdi-account-hard-hat</v-icon>
      </v-avatar>
      <div v-if="store.counterWelder" class="flex-grow-1">
        <div class="text-caption text-medium-emphasis">Working as</div>
        <div class="text-h5 font-weight-bold">{{ store.counterWelder.welderName }}</div>
        <div class="text-caption text-medium-emphasis">{{ store.counterWelder.welderNo }}</div>
      </div>
      <div v-else class="flex-grow-1" style="min-width:260px; max-width:520px;">
        <div class="text-subtitle-1 font-weight-bold mb-2">Who are you? Tap to choose your name</div>
        <WelderPicker :model-value="null" class="welder-big" @update:model-value="choose" />
      </div>
      <v-btn v-if="store.counterWelder" size="x-large" variant="tonal" prepend-icon="mdi-account-switch" @click="choose(null)">
        Change welder
      </v-btn>
    </v-card-text>
  </v-card>
</template>

<script setup>
  import { useConsumableStore } from '@/store/consumableStore'
  import WelderPicker from '@/components/consumables/shared/WelderPicker.vue'

  const emit = defineEmits(['changed'])

  const store = useConsumableStore()

  function choose(welder) {
    store.setWelder(welder)
    emit('changed', welder)
  }
</script>

<style scoped>
  .welder-big :deep(.v-field) {
    font-size: 1.15rem;
    min-height: 64px;
  }
</style>
