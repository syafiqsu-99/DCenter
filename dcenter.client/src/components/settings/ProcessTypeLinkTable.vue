<template>
  <v-card flat>
    <v-card-item class="px-0">
      <v-card-title>Process → Type Links</v-card-title>
      <v-card-subtitle class="text-wrap">
        Choose which electrode Types appear for each welding Process in the joint card.
      </v-card-subtitle>
    </v-card-item>

    <v-card-text class="px-0">
      <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mb-3"
               closable @click:close="error = ''">{{ error }}</v-alert>
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
          <v-col cols="12" sm="6">
            <v-autocomplete v-model="typesToAdd" :items="addableTypes" label="Add Types"
                            multiple chips closable-chips variant="outlined" density="comfortable"
                            hide-details :disabled="!addableTypes.length" />
          </v-col>
          <v-col cols="12" sm="3" class="d-flex ga-2">
            <v-btn variant="text" :disabled="!addableTypes.length"
                   @click="typesToAdd = [...addableTypes]">Select all</v-btn>
            <v-btn color="primary" variant="flat" :disabled="!typesToAdd.length" :loading="adding"
                   @click="addLinks">Add</v-btn>
          </v-col>
        </v-row>
      </template>
      <div v-else class="text-medium-emphasis mt-4">Select a Process to manage its Types.</div>
    </v-card-text>
  </v-card>
</template>

<script setup>
  import { computed, onMounted, ref, watch } from 'vue';
  import { storeToRefs } from 'pinia';
  import { useCrudApi } from '@/composables/useCrudApi';
  import { useLookupStore } from '@/store/lookupStore';
  import { errorText } from '@/utils/errors';

  const lookupStore = useLookupStore();
  const { options } = storeToRefs(lookupStore);

  const links = ref([]);
  const error = ref('');
  const process = ref(null);
  const typesToAdd = ref([])
  const adding = ref(false);
  const deletingId = ref(null);

  const processOptions = computed(() => options.value.Process ?? []);
  const typeOptions = computed(() => options.value.Type ?? []);

  const linkedForProcess = computed(() => links.value.filter((l) => l.process === process.value));
  const linkedTypes = computed(() => linkedForProcess.value.map((l) => l.type));
  const addableTypes = computed(() => typeOptions.value.filter((t) => !linkedTypes.value.includes(t)));

  const linksApi = useCrudApi('/processtypelinks');

  async function load() {
    links.value = await linksApi.list();
  }

  watch(process, () => { typesToAdd.value = [] });

  async function addLinks() {
    if (!process.value || !typesToAdd.value.length) return
    adding.value = true
    try {
      for (const type of typesToAdd.value) {
        links.value.push(await linksApi.create({ process: process.value, type }))
      }
      typesToAdd.value = []
    } catch (e) {
      error.value = errorText(e, 'Could not add the link.')
    } finally {
      adding.value = false
    }
  }

  async function removeLink(link) {
    deletingId.value = link.id;
    try {
      await linksApi.remove(link.id);
      links.value = links.value.filter((l) => l.id !== link.id);
    } catch (e) {
      error.value = errorText(e, 'Could not remove the link.');
    } finally {
      deletingId.value = null;
    }
  }

  onMounted(async () => {
    try {
      await lookupStore.load();
      await load();
    } catch (e) {
      error.value = errorText(e, 'Could not load the process and type lists. Reload the page to try again.');
    }
  });
</script>
