<template>
  <v-card border flat>
    <v-card-title class="d-flex align-center flex-wrap ga-2">
      <v-icon color="blue-grey" class="me-1">mdi-package-variant-closed</v-icon>
      Electrodes in Normal storage
      <span class="text-caption text-medium-emphasis">Take from the main store to bake</span>
      <v-spacer />
      <v-btn variant="text" prepend-icon="mdi-refresh" :loading="loading" @click="load">Refresh</v-btn>
    </v-card-title>
    <StickyBar class="px-4">
      <v-text-field v-model="search" prepend-inner-icon="mdi-magnify" label="Find electrode (classification or diameter)"
                    clearable variant="outlined" density="comfortable" hide-details />
    </StickyBar>
    <v-card-text>
      <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mb-3">{{ error }}</v-alert>
      <v-row v-if="loading && !items.length" dense>
        <v-col v-for="n in 4" :key="n" cols="12" sm="6" xl="3"><v-skeleton-loader type="card" /></v-col>
      </v-row>
      <div v-else-if="!shown.length" class="text-medium-emphasis text-body-1 py-4">
        {{ items.length ? 'No electrode matches the search.' : 'There are no electrodes in Normal storage.' }}
      </div>
      <v-row v-else dense>
        <v-col v-for="r in shown" :key="r.itemId" cols="12" sm="6" xl="3">
          <v-card border flat height="100%">
            <v-card-text class="d-flex flex-column h-100">
              <div class="d-flex align-start">
                <div class="flex-grow-1">
                  <div class="text-h6 font-weight-bold">{{ r.diaSpec }}</div>
                  <div class="text-caption text-medium-emphasis">{{ r.holdingOvenType ?? 'Holding oven not set' }} · {{ r.lotCount }} lot(s)</div>
                </div>
                <v-chip size="large" label variant="tonal" :color="r.normalKg > 0 ? 'blue-grey' : undefined">{{ kg(r.normalKg) }} kg</v-chip>
              </div>
              <div v-if="r.bakingKg > 0" class="text-body-2 mt-2">
                <v-icon size="small" color="deep-orange">mdi-fire</v-icon> {{ kg(r.bakingKg) }} kg already baking
              </div>
              <v-spacer />
              <v-btn class="mt-3" size="x-large" color="purple" variant="flat" block prepend-icon="mdi-fire"
                     :disabled="r.normalKg <= 0" @click="openBake(r)">Send to baking</v-btn>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </v-card-text>
  </v-card>

  <SendToBakeDialog v-model="bakeOpen" :item="selected" @saved="onSent" />
</template>

<script setup>
  import { computed, onMounted, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { ELECTRODE, errorText, kg } from '@/utils/consumables'
  import StickyBar from '@/components/common/StickyBar.vue'
  import SendToBakeDialog from '@/components/consumables/baking/SendToBakeDialog.vue'

  const emit = defineEmits(['sent'])

  const store = useConsumableStore()
  const items = ref([])
  const loading = ref(false)
  const error = ref('')
  const search = ref('')
  const bakeOpen = ref(false)
  const selected = ref(null)

  const shown = computed(() => {
    const terms = (search.value ?? '').trim().toLowerCase().split(/\s+/).filter(Boolean)
    return items.value.filter((r) => terms.every((t) => r.diaSpec.toLowerCase().includes(t)))
  })

  async function load() {
    loading.value = true
    error.value = ''
    try {
      items.value = await store.loadNormalStock(ELECTRODE)
    } catch (e) {
      error.value = errorText(e, 'Could not load Normal storage.')
    } finally {
      loading.value = false
    }
  }

  function openBake(r) {
    selected.value = { ...r }
    bakeOpen.value = true
  }

  async function onSent(result) {
    emit('sent', result)
    await load()
  }

  defineExpose({ load })
  onMounted(load)
</script>
