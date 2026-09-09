import 'vuetify/styles';
import '@mdi/font/css/materialdesignicons.css';
import { createVuetify } from 'vuetify';
import * as components from 'vuetify/components';
import * as directives from 'vuetify/directives';

// Register vuetify components and directives explicitly so components like
// v-app-bar, v-app-bar-title, v-app-bar-nav-icon are available.
export default createVuetify({
  components,
  directives,
  theme: {
    defaultTheme: 'weldTheme',
    themes: {
      weldTheme: {
        dark: false,
        colors: {
          primary: '#00609C',
          secondary: '#455A64',
          surface: '#FFFFFF',
          background: '#F5F6F8',
          error: '#C62828',
        },
      },
    },
  },
});
