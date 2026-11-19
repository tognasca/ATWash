import {chromium} from 'playwright';
import assert from 'node:assert/strict';
import {preview} from 'vite';
const server=await preview({preview:{host:'127.0.0.1',port:4177,strictPort:true}});
let browser;
try{
 const origin='http://127.0.0.1:4177';
 browser=await chromium.launch();
 const context=await browser.newContext({viewport:{width:1200,height:900}});
 const page=await context.newPage();
 page.setDefaultTimeout(9000);
 const token='test.'+Buffer.from(JSON.stringify({tenant_id:'11111111-1111-4111-8111-111111111111',exp:Math.floor(Date.now()/1000)+3600})).toString('base64url')+'.test';
 await page.addInitScript(token=>sessionStorage.setItem('atwash-session',JSON.stringify({token,role:'Owner'})),token);
 const data={
  customers:[{id:'customer',name:'Cliente Flex'}],
  vehicles:[{id:'vehicle',customerId:'customer',plate:'ABC1D23',model:'SUV'}],
  services:[{id:'wash',name:'Lavar',price:40,categoryPrices:{Suv:65}},{id:'wax',name:'Encerar',price:20}],
  branches:[{id:'branch',name:'Unidade Centro'}],employees:[],products:[],orders:[],
  'flex/settings':{stageNames:['Recepção','Lavagem','Secagem','Liberado'],packages:[{name:'Completo',serviceIds:['wash','wax']}]}
 };
 let synced;
 let offline=false;
 await context.route('**/api/**',async route=>{
  if(offline)return route.abort();
  const path=new URL(route.request().url()).pathname.slice(5);
  if(path==='orders'&&route.request().method()==='POST'){
   const body=route.request().postDataJSON();
   data.orders=[{id:body.clientId,number:'000001',vehicleId:body.vehicleId,status:0,paymentStatus:0,total:85,branchId:body.branchId,vehicleCategory:body.vehicleCategory}];
   synced=body;
   return route.fulfill({status:201,json:{id:body.clientId,number:'000001',total:85}});
  }
  await route.fulfill({json:data[path]??[]});
 });
 await page.goto(origin+'/app');
 await page.getByRole('button',{name:'Recepção',exact:true}).waitFor();
 await page.evaluate(async()=>{await navigator.serviceWorker.ready});
 await page.waitForFunction(()=>navigator.serviceWorker.controller!==null);
 offline=true;
 await context.setOffline(true);
 await page.reload();
 await page.getByText('● Offline',{exact:true}).waitFor();
 await page.getByLabel('Encontrar cliente').fill('Cliente Flex');
 await page.getByRole('button',{name:/Cliente Flex/}).click();
 await page.getByLabel('Categoria do veículo').selectOption('3');
 await page.getByLabel('Adicionar pacote').selectOption('Completo');
 await page.locator('#aw-cart').getByLabel('Unidade').selectOption('branch');
 await page.getByRole('button',{name:/Abrir comanda.*85/}).click();
 await page.getByRole('button',{name:'Sincronizar pendências (1)'}).waitFor();
 assert.equal(synced,undefined);
 offline=false;
 await context.setOffline(false);
 await page.getByRole('button',{name:'Sincronizar pendências (0)'}).waitFor();
 const {clientId,...order}=synced;
 assert.match(clientId,/^[0-9a-f-]{36}$/i);
 assert.deepEqual(order,{vehicleId:'vehicle',serviceIds:['wash','wax'],branchId:'branch',vehicleCategory:3,expectedTotal:85});
 console.log('PASS: Chromium PWA reload offline, real IndexedDB, stage labels, package pricing, unit selection and automatic reconnect sync.');
}finally{
 if(browser)await browser.close();
 server.httpServer.close();
}
