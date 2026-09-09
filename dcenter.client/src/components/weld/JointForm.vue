<template>
  <div style="background:#fff;padding:12px;border:1px solid #000;margin-bottom:16px;overflow-x:auto;">
    <div style="font-weight:bold;font-size:14px;margin-bottom:6px;">Joint {{ joint.jointNumber }}</div>

    <table style="border-collapse:collapse;width:100%;table-layout:fixed;">
      <tbody>
        <!-- Section headers -->
        <tr>
          <td :style="labelCell + 'width:16%;'">Joining of</td>
          <td :style="cell + 'width:18%;'"></td>
          <td :style="labelCell + 'width:16%;'">with</td>
          <td :style="cell + 'width:18%;'"></td>
          <td :style="labelCell + 'width:16%;'">Electrode Data:</td>
          <td :style="cell + 'width:16%;text-align:center;font-weight:bold;'"></td>
        </tr>

        <!-- Part Desc -->
        <tr>
          <td :style="labelCell">Part Desc. :</td>
          <td :style="cell">
            <select v-model="joint.partDescLeft" :style="input">
              <option value=""></option>
              <option v-for="p in leftParts" :key="'ldl'+p.no" :value="p.desc">{{ p.desc }}</option>
            </select>
          </td>
          <td :style="labelCell">Part Desc. :</td>
          <td :style="cell">
            <select v-model="joint.partDescRight" :style="input">
              <option value=""></option>
              <option v-for="p in rightParts" :key="'rdr'+p.no" :value="p.desc">{{ p.desc }}</option>
            </select>
          </td>
          <td :style="labelCell">Process</td>
          <td :style="cell">
            <select v-model="joint.materials[0].process" :style="input">
              <option value=""></option>
              <option v-for="v in withCurrent('Process', joint.materials[0].process)" :key="v" :value="v">{{ v }}</option>
            </select>
          </td>
        </tr>

        <!-- Part No -->
        <tr>
          <td :style="labelCell">Part No. :</td>
          <td :style="cell">
            <select v-model="joint.partNoLeft" :style="input">
              <option value=""></option>
              <option v-for="p in leftParts" :key="'lnl'+p.no" :value="p.no">{{ p.no }}</option>
            </select>
          </td>
          <td :style="labelCell">Part No. :</td>
          <td :style="cell">
            <select v-model="joint.partNoRight" :style="input">
              <option value=""></option>
              <option v-for="p in rightParts" :key="'rnr'+p.no" :value="p.no">{{ p.no }}</option>
            </select>
          </td>
          <td :style="labelCell">Size</td>
          <td :style="cell">
            <select v-model="joint.materials[0].size" :style="input">
              <option value=""></option>
              <option v-for="v in withCurrent('Size', joint.materials[0].size)" :key="v" :value="v">{{ v }}</option>
            </select>
          </td>
        </tr>

        <!-- Heat Number -->
        <tr>
          <td :style="labelCell">Heat Number :</td>
          <td :style="cell"><input v-model="joint.heatNumberLeft" :style="input" /></td>
          <td :style="labelCell">Heat Number :</td>
          <td :style="cell"><input v-model="joint.heatNumberRight" :style="input" /></td>
          <td :style="labelCell">Type</td>
          <td :style="cell">
            <select v-model="joint.materials[0].type" :style="input">
              <option value=""></option>
              <option v-for="v in withCurrent('Type', joint.materials[0].type)" :key="v" :value="v">{{ v }}</option>
            </select>
          </td>
        </tr>

        <!-- WPS / Rev / Manuf -->
        <tr>
          <td :style="labelCell">WPS No.:</td>
          <td :style="cell"><input v-model="joint.wpsNo" :style="input" /></td>
          <td :style="labelCell">Rev:</td>
          <td :style="cell"><input v-model="joint.rev" :style="input" /></td>
          <td :style="labelCell">Manuf</td>
          <td :style="cell">
            <select v-model="joint.materials[0].manuf" :style="input">
              <option value=""></option>
              <option v-for="v in withCurrent('Manuf', joint.materials[0].manuf)" :key="v" :value="v">{{ v }}</option>
            </select>
          </td>
        </tr>

        <!-- Welder / Heat-Lot -->
        <tr>
          <td :style="labelCell">Welder Name :</td>
          <td :style="cell">
            <input v-model="joint.welderName" list="welderNameList" :style="input" />
          </td>
          <td :style="labelCell">Welder No</td>
          <td :style="cell">
            <input v-model="joint.welderNo" list="welderNoList" :style="input" />
          </td>
          <td :style="labelCell">Heat/Lot</td>
          <td :style="cell"><input v-model="joint.materials[0].heatLot" :style="input" /></td>
        </tr>
      </tbody>
    </table>

    <!-- Shared datalists for autocomplete -->
    <datalist id="welderNameList">
      <option v-for="w in welderItems" :key="w.id" :value="w.welderName" />
    </datalist>
    <datalist id="welderNoList">
      <option v-for="w in welderItems" :key="w.id" :value="w.welderNo" />
    </datalist>
  </div>
</template>

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
  const { leftParts, rightParts } = storeToRefs(reportStore);

  const lookupStore = useLookupStore();
  const { options } = storeToRefs(lookupStore);

  function withCurrent(category, current) {
        const list = options.value[category] ?? [];
        return current && !list.includes(current) ? [current, ...list] : list;
  }

  watch(() => props.joint.partNoLeft, (v) => {
        const hit = leftParts.value.find((p) => p.no === v);
        if (hit) props.joint.partDescLeft = hit.desc;
  });
  watch(() => props.joint.partDescLeft, (v) => {
        const hit = leftParts.value.find((p) => p.desc === v);
        if (hit && props.joint.partNoLeft !== hit.no) props.joint.partNoLeft = hit.no;
  });
  watch(() => props.joint.partNoRight, (v) => {
        const hit = rightParts.value.find((p) => p.no === v);
        if (hit) props.joint.partDescRight = hit.desc;
  });
  watch(() => props.joint.partDescRight, (v) => {
        const hit = rightParts.value.find((p) => p.desc === v);
        if (hit && props.joint.partNoRight !== hit.no) props.joint.partNoRight = hit.no;
  });

  const welderItems = ref([]);
  async function searchWelders(q) {
        const { data } = await api.get('/welders/search', { params: { q: q || '' } });
        welderItems.value = data;
  }
  searchWelders('');

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

  const cell = 'border:1px solid #000;padding:2px 4px;font-size:12px;vertical-align:middle;';
  const labelCell = cell + 'font-weight:bold;white-space:nowrap;';
  const input =
        'width:100%;border:none;outline:none;font-size:12px;font-weight:bold;background:transparent;';
</script>
