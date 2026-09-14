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
          <td :style="cell"><v-combobox v-model="joint.partDescLeft" :items="leftDescs" v-bind="f" clearable /></td>
          <td :style="lbl">Part Desc. :</td>
          <td :style="cell"><v-combobox v-model="joint.partDescRight" :items="rightDescs" v-bind="f" clearable /></td>
          <td :style="lbl">Process</td>
          <td :style="cell"><v-combobox v-model="joint.materials[0].process" :items="opt('Process', joint.materials[0].process)" v-bind="f" clearable /></td>
          <td :style="cell"><v-combobox v-model="joint.materials[1].process" :items="opt('Process', joint.materials[1].process)" v-bind="f" clearable /></td>
          <td :style="cell"><v-combobox v-model="joint.materials[2].process" :items="opt('Process', joint.materials[2].process)" v-bind="f" clearable /></td>
        </tr>

        <!-- Part No | Part No | Size -->
        <tr>
          <td :style="lbl">Part No. :</td>
          <td :style="cell"><v-combobox v-model="joint.partNoLeft" :items="leftNos" v-bind="f" clearable /></td>
          <td :style="lbl">Part No. :</td>
          <td :style="cell"><v-combobox v-model="joint.partNoRight" :items="rightNos" v-bind="f" clearable /></td>
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
          <td :style="cell"><v-combobox v-model="joint.materials[0].type" :items="opt('Type', joint.materials[0].type)" v-bind="f" clearable /></td>
          <td :style="cell"><v-combobox v-model="joint.materials[1].type" :items="opt('Type', joint.materials[1].type)" v-bind="f" clearable /></td>
          <td :style="cell"><v-combobox v-model="joint.materials[2].type" :items="opt('Type', joint.materials[2].type)" v-bind="f" clearable /></td>
        </tr>

        <!-- WPS No | Rev | Manuf -->
        <tr>
          <td :style="lbl">WPS No.:</td>
          <td :style="cell">
            <v-combobox v-bind="f" clearable :custom-filter="allowAll" v-model="joint.wpsNo" :items="wpsNos"
                        @update:search="searchWps" @update:model-value="onWpsPick" />
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
    import api from '@/utils/api';

    const props = defineProps({
      joint: { type: Object, required: true },
    });

    // One shared prop set so every in-cell field looks identical (borderless, compact).
    const f = { density: 'compact', variant: 'plain', hideDetails: true };
    const allowAll = () => true;

    const reportStore = useReportStore();
    const { leftParts, rightParts } = storeToRefs(reportStore);

    const leftDescs = computed(() => leftParts.value.map((p) => p.desc));
    const leftNos = computed(() => leftParts.value.map((p) => p.no));
    const rightDescs = computed(() => rightParts.value.map((p) => p.desc));
    const rightNos = computed(() => rightParts.value.map((p) => p.no));

    const lookupStore = useLookupStore();
    const { options } = storeToRefs(lookupStore);

    function opt(category, current) {
      const list = options.value[category] ?? [];
      return current && !list.includes(current) ? [current, ...list] : list;
    }

    // Fill the partner on a match; clear the partner when this field is emptied.
    // Treats null/'' the same. Value-equality checks make the reverse watcher a no-op.
    function isEmpty(v) {
      return v === null || v === undefined || v === '';
    }

    function pairSync(pairs, srcKey, dstKey, matchBy) {
      watch(() => props.joint[srcKey], (v) => {
        if (isEmpty(v)) {
          if (!isEmpty(props.joint[dstKey])) props.joint[dstKey] = '';
          return;
        }
        const hit = pairs.value.find((p) => p[matchBy] === v);
        if (hit) {
          const other = matchBy === 'no' ? hit.desc : hit.no;
          if (props.joint[dstKey] !== other) props.joint[dstKey] = other;
        }
      });
    }

    // Left column (column 1): no <-> desc.
    pairSync(leftParts, 'partNoLeft', 'partDescLeft', 'no');
    pairSync(leftParts, 'partDescLeft', 'partNoLeft', 'desc');
    // Right column (column 2): no <-> desc.
    pairSync(rightParts, 'partNoRight', 'partDescRight', 'no');
    pairSync(rightParts, 'partDescRight', 'partNoRight', 'desc');

    const welderItems = ref([]);
    const welderNames = computed(() => welderItems.value.map((x) => x.welderName));
    const welderNos = computed(() => welderItems.value.map((x) => x.welderNo));
    async function searchWelders(q) {
      const { data } = await api.get('/welders/search', { params: { q: q || '' } });
      welderItems.value = data;
    }
    searchWelders('');

    // Welder name <-> no: fill on match, clear the partner when emptied (same as parts).
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

    const wpsItems = ref([]);
    const wpsNos = computed(() => wpsItems.value.map((x) => x.wpsNo));
    async function searchWps(q) {
      const { data } = await api.get('/wps/search', { params: { q: q || '' } });
      wpsItems.value = data;
    }
    searchWps('');
    // v-combobox emits a string (custom text) or a selected string; look up rev if known.
    function onWpsPick(val) {
      const no = typeof val === 'object' && val !== null ? val.wpsNo : val;
      const w = wpsItems.value.find((x) => x.wpsNo === no);
      if (w?.rev && !props.joint.rev) props.joint.rev = w.rev;
    }

    // Inline styles matching the bordered spreadsheet look.
    const wrap = 'background:#fff;padding:12px;border:1px solid #000;margin-bottom:16px;overflow-x:auto;';
    const titleStyle = 'font-weight:bold;font-size:14px;margin-bottom:6px;';
    const tbl = 'border-collapse:collapse;width:100%;table-layout:fixed;';
    const cell = 'border:1px solid #000;padding:0 4px;font-size:12px;vertical-align:middle;height:26px;';
    const lbl = 'border:1px solid #000;padding:0 4px;font-size:12px;font-weight:bold;white-space:nowrap;vertical-align:middle;';
    const hdr = 'border:1px solid #000;padding:2px 4px;font-size:12px;font-weight:bold;text-align:center;';
    const hdrC = 'border:1px solid #000;';
</script>
