<script setup>
import { ref, watch } from 'vue';
import { storeToRefs } from 'pinia';
import { useReportStore } from '@/store/reportStore';
import { useLookupStore } from '@/store/lookupStore';
import api from '@/utils/api';

const props = defineProps({
  joint: { type: Object, required: true },
});

const reportStore = useReportStore();
const { partDescOptions, partNoOptions } = storeToRefs(reportStore);

const lookupStore = useLookupStore();
const { options } = storeToRefs(lookupStore);

// Welder autocomplete — both directions (name <-> number) off one search endpoint.
const welderItems = ref([]);
async function searchWelders(q) {
  const { data } = await api.get('/welders/search', { params: { q: q || '' } });
  welderItems.value = data;
}
searchWelders('');

// Picking in either field fills both name and number on the joint.
function pickByName(name) {
  const w = welderItems.value.find((x) => x.welderName === name);
  if (w) props.joint.welderNo = w.welderNo;
}
function pickByNo(no) {
  const w = welderItems.value.find((x) => x.welderNo === no);
  if (w) props.joint.welderName = w.welderName;
}
watch(() => props.joint.welderName, pickByName);
watch(() => props.joint.welderNo, pickByNo);
</script>

<template>
  <v-card flat border class="mb-4">
    <v-card-title>Joint {{ joint.jointNumber }}</v-card-title>
    <v-card-text>
      <v-row dense>
        <v-col cols="12" md="4">
          <div class="text-subtitle-2 mb-1">Joining of</div>
          <v-combobox
            v-model="joint.partDescLeft"
            :items="partDescOptions"
            label="Part Desc."
            density="compact"
          />
          <v-combobox
            v-model="joint.partNoLeft"
            :items="partNoOptions"
            label="Part No."
            density="compact"
          />
          <v-text-field v-model="joint.heatNumberLeft" label="Heat Number" density="compact" />
        </v-col>

        <v-col cols="12" md="4">
          <div class="text-subtitle-2 mb-1">with</div>
          <v-combobox
            v-model="joint.partDescRight"
            :items="partDescOptions"
            label="Part Desc."
            density="compact"
          />
          <v-combobox
            v-model="joint.partNoRight"
            :items="partNoOptions"
            label="Part No."
            density="compact"
          />
          <v-text-field v-model="joint.heatNumberRight" label="Heat Number" density="compact" />
        </v-col>

        <v-col cols="12" md="4">
          <div class="text-subtitle-2 mb-1">WPS / Welder</div>
          <v-text-field v-model="joint.wpsNo" label="WPS No." density="compact" />
          <v-text-field v-model="joint.rev" label="Rev" density="compact" />
          <v-autocomplete
            v-model="joint.welderName"
            :items="welderItems"
            item-title="welderName"
            item-value="welderName"
            label="Welder Name"
            density="compact"
            :custom-filter="() => true"
            @update:search="searchWelders"
          />
          <v-autocomplete
            v-model="joint.welderNo"
            :items="welderItems"
            item-title="welderNo"
            item-value="welderNo"
            label="Welder No."
            density="compact"
            :custom-filter="() => true"
            @update:search="searchWelders"
          />
        </v-col>
      </v-row>

      <v-divider class="my-2" />
      <div class="text-subtitle-2 mb-1">Electrode Data</div>
      <v-table density="compact">
        <thead>
          <tr>
            <th></th>
            <th>1</th>
            <th>2</th>
            <th>3</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td>Process</td>
            <td v-for="m in joint.materials" :key="'p' + m.columnNumber">
              <v-combobox v-model="m.process" :items="options.Process" hide-details density="compact" />
            </td>
          </tr>
          <tr>
            <td>Size</td>
            <td v-for="m in joint.materials" :key="'s' + m.columnNumber">
              <v-combobox v-model="m.size" :items="options.Size" hide-details density="compact" />
            </td>
          </tr>
          <tr>
            <td>Type</td>
            <td v-for="m in joint.materials" :key="'t' + m.columnNumber">
              <v-combobox v-model="m.type" :items="options.Type" hide-details density="compact" />
            </td>
          </tr>
          <tr>
            <td>Manuf</td>
            <td v-for="m in joint.materials" :key="'m' + m.columnNumber">
              <v-combobox v-model="m.manuf" :items="options.Manuf" hide-details density="compact" />
            </td>
          </tr>
          <tr>
            <td>Heat/Lot</td>
            <td v-for="m in joint.materials" :key="'h' + m.columnNumber">
              <v-text-field v-model="m.heatLot" hide-details density="compact" />
            </td>
          </tr>
        </tbody>
      </v-table>
    </v-card-text>
  </v-card>
</template>
