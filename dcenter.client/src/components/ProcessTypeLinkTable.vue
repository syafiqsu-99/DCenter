<template>
  <v-card flat>
    <v-card-item class="px-0">
      <v-card-title>Process → Type Links</v-card-title>
      <v-card-subtitle class="text-wrap">
        Choose which electrode Types appear for each welding Process in the joint card.
      </v-card-subtitle>
    </v-card-item>

    <v-card-text class="px-0">
      <v-row dense>
        <v-col cols="12" sm="4">
          <v-select v-model="process" :items="processOptions" label="Process"
                    variant="outlined" density="comfortable" hide-details clearable />
        </v-col>
      </v-row>

      <template v-if="process">
        <div class="text-subtitle-2 mt-4 mb-2">Types linked to “{{ process }}”</div>

        <div v-if="linkedForProcess.length" class="d-flex flex-wrap ga-2 mb-4">
          <v-chip v-for="l in linkedForProcess" :key="l.id" closable
                  :disabled="deletingId === l.id" @click:close="removeLink(l)">
            {{ l.type }}
          </v-chip>
        </div>
        <div v-else class="text-medium-emphasis mb-4">
          No Types linked yet — the joint card shows every Type for this Process.
        </div>

        <v-row dense align="center">
          <v-col cols="12" sm="4">
            <v-select v-model="typeToAdd" :items="addableTypes" label="Add a Type"
                      variant="outlined" density="comfortable" hide-details
                      :disabled="!addableTypes.length" />
          </v-col>
          <v-col cols="12" sm="2">
            <v-btn color="primary" variant="flat" :disabled="!typeToAdd" :loading="adding"
                   @click="addLink">Add</v-btn>
          </v-col>
        </v-row>
      </template>
      <div v-else class="text-medium-emphasis mt-4">Select a Process to manage its Types.</div>
    </v-card-text>
  </v-card>
</template>

<script setup>
  import { computed, onMounted, ref } from 'vue';
  import { storeToRefs } from 'pinia';
  import api from '@/utils/api';
  import { useLookupStore } from '@/store/lookupStore';

  const lookupStore = useLookupStore();
  const { options } = storeToRefs(lookupStore);

  const links = ref([]);
  const process = ref(null);
  const typeToAdd = ref(null);
  const adding = ref(false);
  const deletingId = ref(null);

  const processOptions = computed(() => options.value.Process ?? []);
  const typeOptions = computed(() => options.value.Type ?? []);

  const linkedForProcess = computed(() => links.value.filter((l) => l.process === process.value));
  const linkedTypes = computed(() => linkedForProcess.value.map((l) => l.type));
  const addableTypes = computed(() => typeOptions.value.filter((t) => !linkedTypes.value.includes(t)));

  async function load() {
    const { data } = await api.get('/processtypelinks');
    links.value = data;
  }

  async function addLink() {
    if (!process.value || !typeToAdd.value) return;
    adding.value = true;
    try {
      const { data } = await api.post('/processtypelinks', { process: process.value, type: typeToAdd.value });
      links.value.push(data);
      typeToAdd.value = null;
    } finally {
      adding.value = false;
    }
  }

  async function removeLink(link) {
    deletingId.value = link.id;
    try {
      await api.delete(`/processtypelinks/${link.id}`);
      links.value = links.value.filter((l) => l.id !== link.id);
    } finally {
      deletingId.value = null;
    }
  }

  onMounted(async () => {
    await lookupStore.load();
    await load();
  });
</script>
