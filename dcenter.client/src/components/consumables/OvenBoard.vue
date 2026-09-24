<template>
  <div>
    <StickyBar class="mb-4">
    <v-card border flat>
      <v-card-text class="d-flex flex-wrap align-center ga-3">
        <v-text-field v-model="search.lot" prepend-inner-icon="mdi-barcode" label="Lot number" clearable v-bind="field"
                      style="max-width:220px;" />
        <v-autocomplete v-model="search.classification" :items="classifications" label="Classification" clearable v-bind="field"
                        style="max-width:240px;" />
        <v-select v-model="search.diameter" :items="diameters" label="Diameter (mm)" clearable v-bind="field" style="max-width:170px;" />
        <v-chip v-if="searching" :color="matchCount ? 'warning' : undefined" variant="tonal" label>
          {{ matchCount }} compartment(s) match
        </v-chip>
        <v-spacer />
        <div class="d-flex align-center ga-3 text-caption">
          <span class="legend legend--occupied" />Occupied
          <span class="legend legend--empty" />Empty
        </div>
        <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="store.loadingOvens" @click="load(true)">Refresh</v-btn>
      </v-card-text>
      <v-divider />
      <div class="px-4 py-2 text-body-2">
        {{ occupied }} of {{ compartments.length }} compartments in use · {{ kg(totalKg) }} kg in holding
      </div>
    </v-card>
    </StickyBar>
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mb-3">{{ error }}</v-alert>

    <v-card v-if="!readonly && store.ovenBoard.unassigned.length" border flat color="warning" variant="tonal" class="mb-4">
      <v-card-title class="text-subtitle-1"><v-icon class="me-2">mdi-alert</v-icon>Activated electrodes not in any compartment</v-card-title>
      <v-card-text>
        <div v-for="lot in store.ovenBoard.unassigned" :key="lot.lotId" class="d-flex align-center flex-wrap ga-2 py-1">
          <strong>{{ lot.diaSpec }}</strong>
          <span class="text-caption">{{ lot.brand }} · Lot {{ lot.lotNumber }} · {{ kg(lot.kg) }} kg</span>
          <v-spacer />
          <v-btn size="small" variant="flat" color="indigo" prepend-icon="mdi-archive-arrow-down"
                 @click="openMove(lot, { id: null, code: UNASSIGNED })">Put in compartment</v-btn>
        </div>
      </v-card-text>
    </v-card>

    <v-row v-if="store.loadingOvens && !store.ovenBoard.ovens.length">
      <v-col v-for="n in 4" :key="n" cols="12" md="6"><v-skeleton-loader type="image" /></v-col>
    </v-row>
    <v-row v-else>
      <v-col v-for="oven in store.ovenBoard.ovens" :key="oven.id" cols="12" md="6">
        <div class="d-flex align-baseline mb-2">
          <span class="text-subtitle-1 font-weight-bold text-decoration-underline">{{ oven.ovenType }}</span>
          <v-spacer />
          <span class="text-caption text-medium-emphasis">
            {{ oven.compartments.filter((c) => c.contents.length).length }}/{{ oven.compartments.length }} in use
          </span>
        </div>
        <OvenLayout :oven="oven" :is-match="searching ? isMatch : null" @select="openDrawer(oven, $event)" />
      </v-col>
    </v-row>
  </div>

  <CompartmentDrawer v-model="drawerOpen" :oven="drawerOven" :compartment="drawerCompartment" :readonly="readonly"
                     @changed="load(true)" />
  <MoveDialog v-if="!readonly" v-model="moveOpen" :lot="moveLot" :from="moveFrom" @saved="load(true)" />
</template>

<script setup>
  import { computed, onMounted, reactive, ref, watch } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { UNASSIGNED, errorText, kg } from '@/utils/consumables'
  import StickyBar from '@/components/StickyBar.vue'
  import OvenLayout from '@/components/consumables/OvenLayout.vue'
  import CompartmentDrawer from '@/components/consumables/CompartmentDrawer.vue'
  import MoveDialog from '@/components/consumables/MoveDialog.vue'

  defineProps({ readonly: { type: Boolean, default: false } })

  const store = useConsumableStore()
  const field = { variant: 'outlined', density: 'compact', hideDetails: true }
  const search = reactive({ lot: '', classification: null, diameter: null })
  const error = ref('')
  const drawerOpen = ref(false)
  const drawerOven = ref(null)
  const drawerCompartmentId = ref(null)
  const moveOpen = ref(false)
  const moveLot = ref(null)
  const moveFrom = ref(null)

  const compartments = computed(() => store.ovenBoard.ovens.flatMap((o) => o.compartments))
  const contents = computed(() => compartments.value.flatMap((c) => c.contents))
  const classifications = computed(() => [...new Set(contents.value.map((x) => x.specification))].sort())
  const diameters = computed(() => [...new Set(contents.value.map((x) => x.diameter))].sort((a, b) => Number(a) - Number(b)))
  const occupied = computed(() => compartments.value.filter((c) => c.contents.length).length)
  const totalKg = computed(() => compartments.value.reduce((s, c) => s + c.totalKg, 0))
  const searching = computed(() => !!(search.lot ?? '').trim() || !!search.classification || !!search.diameter)
  const matchCount = computed(() => compartments.value.filter(isMatch).length)
  const drawerCompartment = computed(() => compartments.value.find((c) => c.id === drawerCompartmentId.value) ?? null)

  function isMatch(c) {
    const lot = (search.lot ?? '').trim().toLowerCase()
    return c.contents.some((x) =>
      (!lot || x.lotNumber.toLowerCase().includes(lot))
      && (!search.classification || x.specification === search.classification)
      && (!search.diameter || x.diameter === search.diameter))
  }

  async function load(force = false) {
    error.value = ''
    try {
      await store.loadOvens(force)
    } catch (e) {
      error.value = errorText(e, 'Could not load the holding ovens.')
    }
  }

  function openDrawer(oven, c) {
    drawerOven.value = oven
    drawerCompartmentId.value = c.id
    drawerOpen.value = true
  }

  function openMove(lot, from) {
    moveLot.value = lot
    moveFrom.value = from
    moveOpen.value = true
  }

  watch(drawerOpen, (open) => {
    if (!open) drawerCompartmentId.value = null
  })

  onMounted(() => load(true))
</script>

<style scoped>
  .legend {
    display: inline-block;
    width: 14px;
    height: 14px;
    margin-right: 4px;
    border: 1px solid rgb(var(--v-theme-on-surface));
    vertical-align: middle;
  }
  .legend--occupied {
    background: rgba(var(--v-theme-primary), 0.14);
    box-shadow: inset 3px 0 0 rgb(var(--v-theme-primary));
  }
  .legend--empty {
    background: rgb(var(--v-theme-surface));
  }
</style>
