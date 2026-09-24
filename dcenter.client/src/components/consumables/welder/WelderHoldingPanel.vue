<template>
  <v-card border flat class="mb-4">
    <v-card-title class="d-flex align-center">
      <v-icon color="success" class="me-2">mdi-check-circle</v-icon>
      Baked — ready to place
      <v-spacer />
      <v-btn variant="text" prepend-icon="mdi-refresh" :loading="store.loadingBaking" @click="load">Refresh</v-btn>
    </v-card-title>
    <v-card-text>
      <v-alert v-if="error" type="error" variant="tonal" density="compact">{{ error }}</v-alert>
      <div v-else-if="!ready.length" class="text-medium-emphasis text-body-1">Nothing is waiting to be placed. Finish baking first.</div>
      <v-row v-else dense>
        <v-col v-for="r in ready" :key="r.id" cols="12" sm="6" xl="4">
          <v-card border flat>
            <v-card-text class="d-flex align-center ga-3">
              <div class="flex-grow-1">
                <div class="text-h6 font-weight-bold">{{ r.diaSpec }}</div>
                <div class="text-body-2">Lot {{ r.lotNumber }} · {{ kg(r.balanceKg) }} kg</div>
                <div class="text-caption text-medium-emphasis">{{ r.bakingNo }} · {{ r.holdingOvenType ?? 'No oven type set' }}</div>
              </div>
              <v-btn size="x-large" color="indigo" variant="flat" prepend-icon="mdi-archive-arrow-down" @click="openPlace(r)">Place</v-btn>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </v-card-text>
  </v-card>

  <OvenBoard :key="boardKey" readonly />

  <PlaceDialog v-model="placeOpen" :record="current" @saved="onSaved" />
  <v-snackbar v-model="snackbar" :color="snackbarColor" :timeout="snackbarColor === 'warning' ? 8000 : 4000" location="top">
    <span class="text-body-1">{{ snackbarText }}</span>
  </v-snackbar>
</template>

<script setup>
  import { computed, onMounted, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { errorText, kg } from '@/utils/consumables'
  import OvenBoard from '@/components/consumables/holding/OvenBoard.vue'
  import PlaceDialog from '@/components/consumables/baking/PlaceDialog.vue'

  const store = useConsumableStore()
  const placeOpen = ref(false)
  const current = ref(null)
  const boardKey = ref(0)
  const snackbar = ref(false)
  const snackbarText = ref('')
  const snackbarColor = ref('success')
  const error = ref('')

  const ready = computed(() => store.bakingBoard.filter((r) => ['Baked', 'Rebaked'].includes(r.status) && r.balanceKg > 0))

  async function load() {
    error.value = ''
    try {
      await store.loadBakingBoard()
    } catch (e) {
      error.value = errorText(e, 'Could not load the baking board.')
    }
  }

  function openPlace(r) {
    current.value = r
    placeOpen.value = true
  }

  async function onSaved(result) {
    snackbarColor.value = result?.warning ? 'warning' : 'success'
    snackbarText.value = result?.warning
      ?? `${result.holding.holdingNo} saved — ${result.holding.isFinishedAfterBaking ? 'issued to welder' : `placed in ${result.holding.compartmentLabel}`}`
    snackbar.value = true
    boardKey.value += 1
    await load()
  }

  onMounted(load)
</script>
