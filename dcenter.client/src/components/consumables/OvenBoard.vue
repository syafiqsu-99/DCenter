<template>
  <div class="fill-card ga-3">
    <v-card border flat class="flex-shrink-0">
      <v-card-text class="d-flex flex-wrap align-center ga-3 py-3">
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
      <div class="px-4 py-1 text-body-2">
        {{ occupied }} of {{ compartments.length }} compartments in use · {{ kg(totalKg) }} kg in holding
      </div>
    </v-card>
    <v-alert v-if="error" type="error" variant="tonal" density="compact" class="flex-shrink-0">{{ error }}</v-alert>

    <v-expansion-panels v-if="!readonly && store.ovenBoard.unassigned.length" class="flex-shrink-0">
      <v-expansion-panel bg-color="orange-lighten-5" elevation="0" class="border">
        <v-expansion-panel-title class="py-2" style="min-height:40px;">
          <v-icon class="me-2" color="warning">mdi-alert</v-icon>
          {{ store.ovenBoard.unassigned.length }} activated electrode lot(s) not in any compartment
        </v-expansion-panel-title>
        <v-expansion-panel-text>
          <div v-for="lot in store.ovenBoard.unassigned" :key="lot.lotId" class="d-flex align-center flex-wrap ga-2 py-1">
            <strong>{{ lot.diaSpec }}</strong>
            <span class="text-caption">{{ lot.brand }} · Lot {{ lot.lotNumber }} · {{ kg(lot.kg) }} kg</span>
            <v-spacer />
            <v-btn size="small" variant="flat" color="indigo" prepend-icon="mdi-archive-arrow-down"
                   @click="openMove(lot, { id: null, code: UNASSIGNED })">Put in compartment</v-btn>
          </div>
        </v-expansion-panel-text>
      </v-expansion-panel>
    </v-expansion-panels>

    <div class="oven-grid fill">
      <template v-if="store.loadingOvens && !store.ovenBoard.ovens.length">
        <v-skeleton-loader v-for="n in 4" :key="n" type="image" class="h-100" />
      </template>
      <section v-for="oven in store.ovenBoard.ovens" v-else :key="oven.id" class="oven-panel">
        <div class="d-flex align-baseline mb-1">
          <span class="text-subtitle-2 font-weight-bold">{{ oven.ovenType }}</span>
          <v-spacer />
          <span class="text-caption text-medium-emphasis">
            {{ oven.compartments.filter((c) => c.contents.length).length }}/{{ oven.compartments.length }} in use
          </span>
        </div>
        <OvenLayout :oven="oven" :is-match="searching ? isMatch : null"
                    :class="readonly ? undefined : 'oven-panel__layout oven-layout--fill'"
                    @select="openDrawer(oven, $event)" />
      </section>
    </div>
  </div>

  <CompartmentDrawer v-model="drawerOpen" :oven="drawerOven" :compartment="drawerCompartment" :readonly="readonly"
                     @changed="load(true)" />
  <MoveDialog v-if="!readonly" v-model="moveOpen" :lot="moveLot" :from="moveFrom" @saved="load(true)" />
</template>

<script setup>
  import { computed, onMounted, reactive, ref, watch } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { UNASSIGNED, errorText, kg } from '@/utils/consumables'
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
    border: 1px solid #b0bec5;
    vertical-align: middle;
  }
  .legend--occupied {
    background: #fff3e0;
    box-shadow: inset 3px 0 0 #ef6c00;
  }
  .legend--empty {
    background: #ffffff;
  }
  .oven-grid {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    grid-template-rows: repeat(2, minmax(0, 1fr));
    gap: 12px;
  }
  .oven-panel {
    display: flex;
    flex-direction: column;
    min-height: 0;
  }
  .oven-panel__layout {
    flex: 1 1 auto;
    min-height: 0;
  }
  @media (max-width: 959px) {
    .oven-grid {
      grid-template-columns: minmax(0, 1fr);
      grid-template-rows: none;
      overflow: visible;
    }
  }
</style>
