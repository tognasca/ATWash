import type { CapacitorConfig } from '@capacitor/cli';
const config: CapacitorConfig = {
 appId: 'br.com.atwash.app',
 appName: 'ATWashControl',
 webDir: 'dist',
 server: {
  // Android app loads its bundled assets. No cleartext HTTP to remote web servers.
  androidScheme: 'https'
 }
};
export default config;
