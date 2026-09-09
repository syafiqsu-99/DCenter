<template>
  <v-app>
    <v-app-bar color="primary" flat>
      <v-app-bar-nav-icon @click="drawer = !drawer" />
      <v-app-bar-title>Weld Report System</v-app-bar-title>
    </v-app-bar>

    <v-navigation-drawer v-model="drawer">
      <v-list nav>
        <v-list-item
          v-for="l in links"
          :key="l.to"
          :to="l.to"
          :prepend-icon="l.icon"
          :title="l.title"
        />
      </v-list>
    </v-navigation-drawer>

    <v-main>
      <v-container fluid>
        <router-view />
      </v-container>
    </v-main>
  </v-app>
</template>

<script setup>
  import { ref } from 'vue';

  const drawer = ref(true);
  const links = [
      { title: 'Weld Reports', to: '/', icon: 'mdi-file-document-edit-outline' },
      { title: 'Welders', to: '/welders', icon: 'mdi-account-hard-hat' },
      { title: 'Dropdown Lists', to: '/lookups', icon: 'mdi-format-list-bulleted' },
  ];
</script>
