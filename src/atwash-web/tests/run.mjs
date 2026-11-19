import {build} from 'esbuild';

const {outputFiles}=await build({
 entryPoints:[new URL('./offline.test.ts',import.meta.url).pathname],
 bundle:true,
 write:false,
 format:'esm',
 platform:'node',
 define:{'import.meta.env.VITE_API_BASE_URL':'undefined'}
});
await import('data:text/javascript;base64,'+Buffer.from(outputFiles[0].contents).toString('base64'));
