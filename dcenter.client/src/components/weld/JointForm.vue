<template>
  <div :style="wrap">
    <div :style="titleStyle">Joint {{ joint.jointNumber }}</div>

    <table :style="tbl">
      <colgroup>
        <col style="width:12%;" />
        <col style="width:19%;" />
        <col style="width:12%;" />
        <col style="width:19%;" />
        <col style="width:12%;" />
        <col style="width:8.66%;" />
        <col style="width:8.66%;" />
        <col style="width:8.66%;" />
      </colgroup>
      <tbody>
        <!-- Section header row -->
        <tr>
          <td :style="hdr" colspan="2">Joining of</td>
          <td :style="hdr" colspan="2">with</td>
          <td :style="hdr">Electrode Data:</td>
          <td :style="hdrC"></td>
          <td :style="hdrC"></td>
          <td :style="hdrC"></td>
        </tr>

        <!-- Part Desc | Part Desc | Process -->
        <tr>
          <td :style="lbl">Part Desc. :</td>
          <td :style="cell">
            <v-combobox v-bind="f" clearable :items="rightParts"
                        :model-value="joint.partDescLeft" :item-title="partDescTitle"
                        @update:model-value="(v) => setPart('Left', 'desc', v)">
              <template #item="{ props: p, item }">
                <v-list-item v-bind="p" :title="item.raw?.no ?? item.raw" :subtitle="item.raw?.desc" />
              </template>
            </v-combobox>
          </td>
          <td :style="lbl">Part Desc. :</td>
          <td :style="cell">
            <v-combobox v-bind="f" clearable :items="rightParts"
                        :model-value="joint.partDescRight" :item-title="partDescTitle"
                        @update:model-value="(v) => setPart('Right', 'desc', v)">
              <template #item="{ props: p, item }">
                <v-list-item v-bind="p" :title="item.raw?.no ?? item.raw" :subtitle="item.raw?.desc" />
              </template>
            </v-combobox>
          </td>
          <td :style="lbl">Process</td>
          <td :style="cell"><v-combobox v-model="joint.materials[0].process" :items="opt('Process', joint.materials[0].process)" v-bind="f" clearable /></td>
          <td :style="cell"><v-combobox v-model="joint.materials[1].process" :items="opt('Process', joint.materials[1].process)" v-bind="f" clearable /></td>
          <td :style="cell"><v-combobox v-model="joint.materials[2].process" :items="opt('Process', joint.materials[2].process)" v-bind="f" clearable /></td>
        </tr>

        <!-- Part No | Part No | Size -->
        <tr>
          <td :style="lbl">Part No. :</td>
          <td :style="cell">
            <v-combobox v-bind="f" clearable :items="rightParts"
                        :model-value="joint.partNoLeft" :item-title="partNoTitle"
                        @update:model-value="(v) => setPart('Left', 'no', v)">
              <template #item="{ props: p, item }">
                <v-list-item v-bind="p" :title="item.raw?.no ?? item.raw" :subtitle="item.raw?.desc" />
              </template>
            </v-combobox>
          </td>
          <td :style="lbl">Part No. :</td>
          <td :style="cell">
            <v-combobox v-bind="f" clearable :items="rightParts"
                        :model-value="joint.partNoRight" :item-title="partNoTitle"
                        @update:model-value="(v) => setPart('Right', 'no', v)">
              <template #item="{ props: p, item }">
                <v-list-item v-bind="p" :title="item.raw?.no ?? item.raw" :subtitle="item.raw?.desc" />
              </template>
            </v-combobox>
          </td>
          <td :style="lbl">Size</td>
          <td :style="cell"><v-combobox v-model="joint.materials[0].size" :items="opt('Size', joint.materials[0].size)" v-bind="f" clearable /></td>
          <td :style="cell"><v-combobox v-model="joint.materials[1].size" :items="opt('Size', joint.materials[1].size)" v-bind="f" clearable /></td>
          <td :style="cell"><v-combobox v-model="joint.materials[2].size" :items="opt('Size', joint.materials[2].size)" v-bind="f" clearable /></td>
        </tr>

        <!-- Heat Number | Heat Number | Type -->
        <tr>
          <td :style="lbl">Heat Number :</td>
          <td :style="cell"><v-text-field v-model="joint.heatNumberLeft" v-bind="f" /></td>
          <td :style="lbl">Heat Number :</td>
          <td :style="cell"><v-text-field v-model="joint.heatNumberRight" v-bind="f" /></td>
          <td :style="lbl">Type</td>
          <td :style="cell"><v-combobox v-model="joint.materials[0].type" :items="typeOpt(0)" v-bind="f" clearable /></td>
          <td :style="cell"><v-combobox v-model="joint.materials[1].type" :items="typeOpt(1)" v-bind="f" clearable /></td>
          <td :style="cell"><v-combobox v-model="joint.materials[2].type" :items="typeOpt(2)" v-bind="f" clearable /></td>
        </tr>

        <!-- WPS No | Rev | Manuf -->
        <tr>
          <td :style="lbl">WPS No.:</td>
          <td :style="cell">
            <v-combobox v-bind="f" clearable :custom-filter="allowAll" v-model="joint.wpsNo" :items="wpsNos"
                        :loading="loadingWpsFilter" :messages="wpsHint" @update:search="searchWps" />
          </td>
          <td :style="lbl">Rev:</td>
          <td :style="cell"><v-text-field v-model="joint.rev" v-bind="f" /></td>
          <td :style="lbl">Manuf</td>
          <td :style="cell"><v-combobox v-model="joint.materials[0].manuf" :items="opt('Manuf', joint.materials[0].manuf)" v-bind="f" clearable /></td>
          <td :style="cell"><v-combobox v-model="joint.materials[1].manuf" :items="opt('Manuf', joint.materials[1].manuf)" v-bind="f" clearable /></td>
          <td :style="cell"><v-combobox v-model="joint.materials[2].manuf" :items="opt('Manuf', joint.materials[2].manuf)" v-bind="f" clearable /></td>
        </tr>

        <!-- Welder Name | Welder No | Heat/Lot -->
        <tr>
          <td :style="lbl">Welder Name :</td>
          <td :style="cell">
            <v-combobox v-bind="f" clearable :custom-filter="allowAll" v-model="joint.welderName" :items="welderNames"
                        @update:search="searchWelders" />
          </td>
          <td :style="lbl">Welder No :</td>
          <td :style="cell">
            <v-combobox v-bind="f" clearable :custom-filter="allowAll" v-model="joint.welderNo" :items="welderNos"
                        @update:search="searchWelders" />
          </td>
          <td :style="lbl">Heat/Lot</td>
          <td :style="cell"><v-text-field v-model="joint.materials[0].heatLot" v-bind="f" /></td>
          <td :style="cell"><v-text-field v-model="joint.materials[1].heatLot" v-bind="f" /></td>
          <td :style="cell"><v-text-field v-model="joint.materials[2].heatLot" v-bind="f" /></td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<script setup>
  import { computed, ref, watch } from 'vue';
  import { storeToRefs } from 'pinia';
  import { useReportStore } from '@/store/reportStore';
  import { useLookupStore } from '@/store/lookupStore';
  import { useProcessTypeStore } from '@/store/processTypeStore';
  import api from '@/utils/api';

  const props = defineProps({
    joint: { type: Object, required: true },
  });

  const processTypeStore = useProcessTypeStore()
  processTypeStore.load()

  function typeOpt(col) {
    const linked = processTypeStore.typesForProcess(props.joint.materials[col].process)
    const base = linked ?? (options.value['Type'] ?? [])
    const current = props.joint.materials[col].type
    return current && !base.includes(current) ? [current, ...base] : base
  }

  const f = { density: 'compact', variant: 'plain', hideDetails: true };
  const allowAll = () => true;

  const reportStore = useReportStore();
  const { rightParts } = storeToRefs(reportStore);

  const lookupStore = useLookupStore();
  const { options } = storeToRefs(lookupStore);

  function opt(category, current) {
    const list = options.value[category] ?? [];
    return current && !list.includes(current) ? [current, ...list] : list;
  }

  function isEmpty(v) {
    return v === null || v === undefined || v === '';
  }

  function partNoTitle(p) {
    return typeof p === 'string' ? p : (p?.no ?? '')
  }
  function partDescTitle(p) {
    return typeof p === 'string' ? p : (p?.desc ?? '')
  }
  function setPart(side, field, v) {
    const j = props.joint
    const noKey = `partNo${side}`
    const descKey = `partDesc${side}`
    if (v && typeof v === 'object') {
      j[noKey] = v.no ?? ''
      j[descKey] = v.desc ?? ''
      return
    }
    const val = v ?? ''
    const hit = rightParts.value.find((p) => (field === 'no' ? p.no : p.desc) === val)
    if (hit) {
      j[noKey] = hit.no
      j[descKey] = hit.desc
    } else {
      j[field === 'no' ? noKey : descKey] = val
    }
  }

  const welderItems = ref([]);
  const welderNames = computed(() => welderItems.value.map((x) => x.welderName));
  const welderNos = computed(() => welderItems.value.map((x) => x.welderNo));
  async function searchWelders(q) {
    const { data } = await api.get('/welders/search', { params: { q: q || '' } });
    welderItems.value = data;
  }
  searchWelders('');

  watch(() => props.joint.welderName, (name) => {
    if (isEmpty(name)) {
      if (!isEmpty(props.joint.welderNo)) props.joint.welderNo = '';
      return;
    }
    const w = welderItems.value.find((x) => x.welderName === name);
    if (w && props.joint.welderNo !== w.welderNo) props.joint.welderNo = w.welderNo;
  });

  watch(() => props.joint.welderNo, (no) => {
    if (isEmpty(no)) {
      if (!isEmpty(props.joint.welderName)) props.joint.welderName = '';
      return;
    }
    const w = welderItems.value.find((x) => x.welderNo === no);
    if (w && props.joint.welderName !== w.welderName) props.joint.welderName = w.welderName;
  });

  const { allowedWps, wpsFilterNote, loadingWpsFilter } = storeToRefs(reportStore);
  const searchResults = ref([]);
  const wpsQuery = ref('');

  const hasFilter = computed(() => allowedWps.value.length > 0);

  const wpsItems = computed(() => {
    if (!hasFilter.value) return searchResults.value;
    const q = wpsQuery.value.trim().toLowerCase();
    if (!q) return allowedWps.value;
    return allowedWps.value.filter(
      (w) => w.wpsNo.toLowerCase().includes(q) || (w.process ?? '').toLowerCase().includes(q));
  });

  const wpsNos = computed(() => [...new Set(wpsItems.value.map((x) => x.wpsNo))]);

  const wpsHint = computed(() => {
    if (hasFilter.value) {
      const pNos = reportStore.filterPNos ?? [];
      return pNos.length ? `Filtered to P-No ${pNos.join(', ')}` : '';
    }
    return wpsFilterNote.value ?? '';
  });

  async function searchWps(q) {
    wpsQuery.value = q || '';
    if (hasFilter.value) return;
    const { data } = await api.get('/wps/search', { params: { q: q || '' } });
    searchResults.value = data;
  }
  searchWps('');

  const wrap = 'background:#fff;padding:12px;border:1px solid #000;margin-bottom:16px;overflow-x:auto;';
  const titleStyle = 'font-weight:bold;font-size:14px;margin-bottom:6px;';
  const tbl = 'border-collapse:collapse;width:100%;table-layout:fixed;';
  const cell = 'border:1px solid #000;padding:0 4px;font-size:12px;vertical-align:middle;height:26px;';
  const lbl = 'border:1px solid #000;padding:0 4px;font-size:12px;font-weight:bold;white-space:nowrap;vertical-align:middle;';
  const hdr = 'border:1px solid #000;padding:2px 4px;font-size:12px;font-weight:bold;text-align:center;';
  const hdrC = 'border:1px solid #000;';
</script>
