<template>
  <v-row>
    <v-col cols="12" lg="8">
      <StickyBar>
      <div class="d-flex flex-wrap align-center ga-3">
        <v-btn-toggle :model-value="store.counterCategory" mandatory divided variant="outlined" color="primary"
                      @update:model-value="selectCategory">
          <v-btn :value="ALL" size="large">All</v-btn>
          <v-btn v-for="c in store.catalog.categories" :key="c" :value="c" size="large">{{ c }}</v-btn>
        </v-btn-toggle>
        <v-text-field v-model="search" prepend-inner-icon="mdi-magnify" :label="isReturn ? 'Find consumable to return' : 'Find consumable'"
                      clearable variant="outlined" density="comfortable" hide-details style="min-width:220px;" />
        <v-btn v-if="isReturn" size="large" variant="tonal" prepend-icon="mdi-magnify-plus-outline" @click="open(null)">
          Not listed
        </v-btn>
      </div>
      </StickyBar>
      <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mb-3">{{ error }}</v-alert>

      <v-row v-if="store.loadingCounter && !store.counterItems.length">
        <v-col v-for="n in 6" :key="n" cols="12" sm="6" xl="4"><v-skeleton-loader type="card" /></v-col>
      </v-row>
      <div v-else-if="!tiles.length" class="text-medium-emphasis text-body-1 text-center py-10">
        {{ isReturn
          ? `Nothing outstanding from the last ${store.catalog.returnWindowDays} days. Use “Not listed” to return something older.`
          : 'Nothing is available to take right now.' }}
      </div>
      <v-row v-else dense>
        <v-col v-for="t in tiles" :key="t.itemId" cols="12" sm="6" xl="4">
          <v-card border flat height="100%" min-height="132" class="tile" :disabled="!isReturn && t.activatedKg <= 0" @click="open(t)">
            <v-card-text class="d-flex flex-column h-100">
              <div class="d-flex align-start">
                <div class="flex-grow-1">
                  <div class="text-h6 font-weight-bold">{{ t.diaSpec }}</div>
                  <div class="text-caption text-medium-emphasis">{{ t.category }}</div>
                </div>
                <v-chip :color="isReturn ? 'teal' : 'info'" size="large" label variant="tonal">
                  {{ isReturn ? `${kg(t.pickedKg - t.returnedKg)} out` : `${kg(t.activatedKg)} kg` }}
                </v-chip>
              </div>
              <div v-if="!isReturn && t.category === ELECTRODE && t.bins.length" class="d-flex flex-wrap ga-1 mt-2">
                <v-chip v-for="b in t.bins" :key="b.compartmentId ?? 'none'" size="small" label variant="tonal"
                        :color="b.compartmentId ? 'indigo' : 'warning'">{{ b.label }} · {{ kg(b.kg) }}</v-chip>
              </div>
              <div v-if="isReturn" class="text-body-2 mt-2">Picked {{ kg(t.pickedKg) }} · returned {{ kg(t.returnedKg) }} kg</div>
              <v-spacer />
              <div class="text-right text-subtitle-2 mt-2" :class="isReturn ? 'text-teal' : 'text-info'">
                Tap to {{ isReturn ? 'return' : 'take' }} <v-icon>mdi-chevron-right</v-icon>
              </div>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </v-col>
    <v-col cols="12" lg="4"><WelderActivityList /></v-col>
  </v-row>

  <CounterDialog v-model="dialogOpen" :mode="isReturn ? 'return' : 'pickup'" :item="selected" :welder="store.counterWelder"
                 @saved="onSaved" />
  <FinishPromptDialog v-model="promptOpen" :residuals="residuals" @done="refresh" />
  <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="snackbarColor === 'warning' ? 8000 : 4000" location="top">
    <span class="text-body-1">{{ snackbarText }}</span>
  </v-snackbar>
</template>

<script setup>
  import { computed, onMounted, ref, watch } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { ALL, ELECTRODE, errorText, kg } from '@/utils/consumables'
  import StickyBar from '@/components/common/StickyBar.vue'
  import CounterDialog from '@/components/consumables/welder/CounterDialog.vue'
  import FinishPromptDialog from '@/components/consumables/welder/FinishPromptDialog.vue'
  import WelderActivityList from '@/components/consumables/welder/WelderActivityList.vue'

  const props = defineProps({ mode: { type: String, default: 'use' } })

  const store = useConsumableStore()
  const search = ref('')
  const error = ref('')
  const selected = ref(null)
  const dialogOpen = ref(false)
  const promptOpen = ref(false)
  const residuals = ref([])
  const snackbar = ref(false)
  const snackbarText = ref('')
  const snackbarColor = ref('success')

  const isReturn = computed(() => props.mode === 'return')

  const tiles = computed(() => {
    const terms = (search.value ?? '').trim().toLowerCase().split(/\s+/).filter(Boolean)
    const source = isReturn.value
      ? store.counterItems.filter((t) => t.pickedKg - t.returnedKg > 0).sort((a, b) => (b.pickedKg - b.returnedKg) - (a.pickedKg - a.returnedKg))
      : store.counterItems.filter((t) => t.activatedKg > 0)
    return source.filter((t) => terms.every((term) => t.diaSpec.toLowerCase().includes(term)))
  })

  async function refresh() {
    error.value = ''
    try {
      await store.loadCounter()
    } catch (e) {
      error.value = errorText(e, 'Could not load consumables.')
    }
  }

  function selectCategory(category) {
    store.counterCategory = category
    refresh()
  }

  function open(tile) {
    selected.value = tile
    dialogOpen.value = true
  }

  async function onSaved(result) {
    snackbarColor.value = result.warning ? 'warning' : 'success'
    snackbarText.value = result.warning
      ?? `Saved ${result.txnNo} — ${kg(result.lines.reduce((s, l) => s + l.quantityKg, 0))} kg ${result.balance.diaSpec}`
    snackbar.value = true
    if (result.residuals?.length) {
      residuals.value = result.residuals
      promptOpen.value = true
    }
    await refresh()
  }

  watch(() => store.counterWelder?.id, refresh)
  onMounted(refresh)
</script>

<style scoped>
  .tile {
    cursor: pointer;
  }
</style>
