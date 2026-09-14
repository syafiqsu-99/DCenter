<template>
  <div :style="wrap">
    <div :style="titleStyle">WELD SHOP JOB REPORT</div>

    <table :style="tbl">
      <tbody>
        <tr>
          <!-- Left: report meta -->
          <td style="width:50%;vertical-align:top;padding-right:12px;">
            <table :style="innerTbl">
              <tbody>
                <tr>
                  <td :style="lbl">Report Required?</td>
                  <td :style="cell">
                    <v-select v-model="report.reportRequired" :items="yesNo" item-title="t"
                              item-value="v" v-bind="f" />
                  </td>
                </tr>
                <tr>
                  <td :style="lbl">Date Welded :</td>
                  <td :style="cell"><v-text-field v-model="report.dateWelded" type="date" v-bind="f" /></td>
                </tr>
                <tr>
                  <td :style="lbl">Work Order:</td>
                  <td :style="cell"><v-text-field v-model="report.workOrder" v-bind="f" /></td>
                </tr>
                <tr>
                  <td :style="lbl">Part No. :</td>
                  <td :style="cell"><v-text-field v-model="report.partNo" v-bind="f" /></td>
                </tr>
                <tr>
                  <td :style="lbl">Description:</td>
                  <td :style="cell"><v-text-field v-model="report.description" v-bind="f" /></td>
                </tr>
              </tbody>
            </table>
          </td>

          <!-- Right: material spec / grade / P# across columns 1-3 -->
          <td style="width:50%;vertical-align:top;">
            <table :style="innerTbl">
              <tbody>
                <tr>
                  <td :style="lbl"></td>
                  <td :style="hdrC">1</td>
                  <td :style="hdrC">2</td>
                  <td :style="hdrC">3</td>
                </tr>
                <tr>
                  <td :style="lblR">Material:</td>
                  <td :style="cell"><v-text-field v-model="report.materialSpec1" v-bind="f" /></td>
                  <td :style="cell"><v-text-field v-model="report.materialSpec2" v-bind="f" /></td>
                  <td :style="cell"><v-text-field v-model="report.materialSpec3" v-bind="f" /></td>
                </tr>
                <tr>
                  <td :style="lblR">Grade:</td>
                  <td :style="cell"><v-text-field v-model="report.grade1" v-bind="f" /></td>
                  <td :style="cell"><v-text-field v-model="report.grade2" v-bind="f" /></td>
                  <td :style="cell"><v-text-field v-model="report.grade3" v-bind="f" /></td>
                </tr>
                <tr>
                  <td :style="lblR">P#:</td>
                  <td :style="cell"><v-text-field v-model="report.pNumber1" v-bind="f" /></td>
                  <td :style="cell"><v-text-field v-model="report.pNumber2" v-bind="f" /></td>
                  <td :style="cell"><v-text-field v-model="report.pNumber3" v-bind="f" /></td>
                </tr>
              </tbody>
            </table>
          </td>
        </tr>
      </tbody>
    </table>
  </div>

  <JointForm v-for="joint in report.joints.slice(0, 9)"
             :key="joint.jointNumber"
             :joint="joint" />
</template>

<script setup>
    import { storeToRefs } from 'pinia';
    import { useReportStore } from '@/store/reportStore';
    import { useLookupStore } from '@/store/lookupStore';
    import JointForm from '@/components/weld/JointForm.vue';

    const store = useReportStore();
    const { report } = storeToRefs(store);

    useLookupStore().load(true);

    const yesNo = [{ t: 'YES', v: true }, { t: 'NO', v: false }];

    // Same field styling as the joint fields.
    const f = { density: 'compact', variant: 'plain', hideDetails: true };

    // Same bordered look as JointForm.
    const wrap = 'background:#fff;padding:12px;border:1px solid #000;margin-bottom:16px;overflow-x:auto;';
    const titleStyle = 'text-align:center;font-weight:bold;font-size:15px;text-decoration:underline;margin-bottom:10px;';
    const tbl = 'border-collapse:collapse;width:100%;';
    const innerTbl = 'border-collapse:collapse;width:100%;table-layout:fixed;';
    const cell = 'border:1px solid #000;padding:0 4px;font-size:12px;vertical-align:middle;height:26px;';
    const lbl = 'border:1px solid #000;padding:0 4px;font-size:12px;font-weight:bold;white-space:nowrap;vertical-align:middle;width:35%;';
    const lblR = lbl + 'text-align:right;';
    const hdrC = 'border:1px solid #000;padding:0 4px;font-size:12px;font-weight:bold;text-align:center;';
</script>
