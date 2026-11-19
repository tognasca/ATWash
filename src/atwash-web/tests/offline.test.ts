import assert from 'node:assert/strict';
import {test} from 'node:test';
import 'fake-indexeddb/auto';
import {createOfflineOrder,loadSnapshot,pendingCount,saveSnapshot,syncPending} from '../src/offline';
import {defaultFlexSettings} from '../src/api';

test('preserves branch, category and quoted total through offline conflict and retry',async()=>{
 const tenant='11111111-1111-4111-8111-111111111111';
 const other='22222222-2222-4222-8222-222222222222';
 const token='x.'+btoa(JSON.stringify({tenant_id:tenant}))+'.x';
 const branch={id:'branch',name:'Centro'};
 const flex={...defaultFlexSettings,packages:[{name:'Completo',serviceIds:['wash','wax']}]};
 await saveSnapshot(tenant,{
  branches:[branch],flex,customers:[],products:[],orders:[],
  vehicles:[{id:'vehicle',customerId:'customer',plate:'ABC1D23',model:'SUV'}],
  services:[{id:'wash',name:'Lavagem',price:40,categoryPrices:{Suv:65}},{id:'wax',name:'Cera',price:20}]
 });
 await assert.rejects(createOfflineOrder(tenant,'vehicle',['wash'],'foreign',3));
 assert.equal(await pendingCount(tenant),0);
 const order=await createOfflineOrder(tenant,'vehicle',flex.packages[0].serviceIds,branch.id,3);
 assert.equal(order.total,85);
 assert.equal((await loadSnapshot(tenant))?.orders[0].branchId,branch.id);
 assert.deepEqual((await loadSnapshot(tenant))?.flex,flex);
 assert.equal(await loadSnapshot(other),undefined);
 assert.equal(await pendingCount(other),0);

 const previousNavigator=Object.getOwnPropertyDescriptor(globalThis,'navigator')!;
 Object.defineProperty(globalThis,'navigator',{configurable:true,value:{
  onLine:true,locks:{request:(_name:unknown,_options:unknown,callback:()=>unknown)=>callback()}
 }});
 const previousFetch=globalThis.fetch;
 let accept=false;
 globalThis.fetch=async(url,init)=>{
  assert.equal(url,'/api/orders');
  assert.deepEqual(JSON.parse(String(init?.body)),{
   vehicleId:'vehicle',serviceIds:['wash','wax'],branchId:branch.id,
   vehicleCategory:3,expectedTotal:85,clientId:order.id
  });
  return accept?Response.json({id:order.id,number:'000001'},{status:201}):new Response('Preço alterado',{status:409});
 };
 try{
  await assert.rejects(syncPending(token),/Preço alterado/);
  assert.equal(await pendingCount(tenant),1);
  accept=true;
  assert.equal(await syncPending(token),1);
  assert.equal(await pendingCount(tenant),0);
  assert.equal((await loadSnapshot(tenant))?.orders[0].number,'000001');
 }finally{
  globalThis.fetch=previousFetch;
  Object.defineProperty(globalThis,'navigator',previousNavigator);
 }
});
