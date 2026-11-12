import {createOperationId} from './operationId';
import {api,type Customer,type Vehicle,type Service,type Product,type Order} from './api';

export type Snapshot = {customers:Customer[];vehicles:Vehicle[];services:Service[];products:Product[];orders:Order[]};
type Pending =
 | {id:string;tenant:string;kind:'status';orderId:string;from:number;to:number;createdAt:number}
 | {id:string;tenant:string;kind:'customer';tempId:string;name:string;phone:string;createdAt:number}
 | {id:string;tenant:string;kind:'vehicle';tempId:string;customerId:string;plate:string;model:string;createdAt:number}
 | {id:string;tenant:string;kind:'order';tempId:string;vehicleId:string;serviceIds:string[];createdAt:number};
const DB_NAME='atwash-offline-v1';
function open():Promise<IDBDatabase>{
 return new Promise((resolve,reject)=>{
  const req=indexedDB.open(DB_NAME,1);
  req.onupgradeneeded=()=>{
   const db=req.result;
   db.createObjectStore('snapshots');
   db.createObjectStore('pending',{keyPath:'id'});
  };
  req.onsuccess=()=>resolve(req.result);
  req.onerror=()=>reject(req.error);
 });
}
async function store<T>(name:string,mode:IDBTransactionMode,fn:(s:IDBObjectStore)=>IDBRequest<T>):Promise<T>{
 const db=await open();
 return new Promise((resolve,reject)=>{
  const tx=db.transaction(name,mode);
  const request=fn(tx.objectStore(name));
  let result:T;
  request.onsuccess=()=>{result=request.result};
  request.onerror=()=>{db.close();reject(request.error)};
  tx.oncomplete=()=>{db.close();resolve(result)};
  tx.onabort=()=>{db.close();reject(tx.error??new Error('Transação offline cancelada.'))};
  tx.onerror=()=>{db.close();reject(tx.error)};
 });
}
export function tenantFromToken(token:string):string{
 try{
  const part=token.split('.')[1];
  const decoded=JSON.parse(atob(part.replace(/-/g,'+').replace(/_/g,'/')));
  if(typeof decoded.tenant_id==='string' && /^[0-9a-f-]{36}$/i.test(decoded.tenant_id))return decoded.tenant_id;
 }catch{/* invalid token */}
 throw new Error('Sessão inválida. Faça login novamente.');
}
export async function saveSnapshot(tenant:string,snapshot:Snapshot){
 await store('snapshots','readwrite',s=>s.put(snapshot,tenant));
}
export async function loadSnapshot(tenant:string):Promise<Snapshot|undefined>{
 const snapshot=await store<Snapshot|undefined>('snapshots','readonly',s=>s.get(tenant));
 if(!snapshot)return undefined;
 // Replay unacknowledged status transitions over the persisted server snapshot.
 // This prevents an interrupted refresh from visually rolling back local work.
 const all=await store<Pending[]>('pending','readonly',s=>s.getAll());
 for(const item of all.filter((x):x is Extract<Pending,{kind:'status'}>=>x.tenant===tenant&&x.kind==='status').sort((a,b)=>a.createdAt-b.createdAt)){
  const order=snapshot.orders.find(o=>o.id===item.orderId);
  if(order&&order.status===item.from)order.status=item.to;
 }
 return snapshot;
}
export async function queueStatus(tenant:string,orderId:string,from:number,to:number){
 if(to!==from+1 || to>3)throw new Error('Apenas avanço de etapas de lavagem pode ser enfileirado offline.');
 const item:Pending={id:newId(),tenant,kind:'status',orderId,from,to,createdAt:Date.now()};
 const db=await open();
 const inserted=await new Promise<boolean>((resolve,reject)=>{
  const tx=db.transaction('pending','readwrite');
  const s=tx.objectStore('pending');
  let added=false;
  const req=s.getAll();
  req.onsuccess=()=>{
   const same=(req.result as Pending[]).filter((x):x is Extract<Pending,{kind:'status'}>=>x.tenant===tenant&&x.kind==='status'&&x.orderId===orderId).sort((a,b)=>a.createdAt-b.createdAt);
   const last=same.at(-1);
   if(last&&last.to!==from){tx.abort();return}
   if(same.some(x=>x.from===from&&x.to===to))return;
   s.add(item);added=true;
  };
  tx.oncomplete=()=>{db.close();resolve(added)};
  tx.onabort=()=>{db.close();reject(new Error('Etapa offline inconsistente. Sincronize antes de continuar.'))};
  tx.onerror=()=>{db.close();reject(tx.error)};
 });
 if(!inserted)return;
 const snapshot=await loadSnapshot(tenant);
 if(snapshot){
  const order=snapshot.orders.find(x=>x.id===orderId);
  if(order){order.status=to;await saveSnapshot(tenant,snapshot)}
 }
}
function newId():string{
 return createOperationId();
}
// A local operation and its visible snapshot are committed together. A crash
// cannot leave a customer, vehicle or order visible without its sync record.
async function appendOffline<T>(tenant:string,kind:Pending,apply:(snapshot:Snapshot)=>T):Promise<T>{
 const db=await open();
 return new Promise<T>((resolve,reject)=>{
  const tx=db.transaction(['snapshots','pending'],'readwrite');
  const snapshots=tx.objectStore('snapshots');
  const req=snapshots.get(tenant);
  let result:T;
  req.onsuccess=()=>{
   const snapshot=req.result as Snapshot|undefined;
   if(!snapshot){tx.abort();return}
   try{
    result=apply(snapshot);
    tx.objectStore('pending').add(kind);
    snapshots.put(snapshot,tenant);
   }catch{tx.abort()}
  };
  tx.oncomplete=()=>{db.close();resolve(result)};
  tx.onabort=()=>{db.close();reject(new Error('Operação offline inválida ou dados locais indisponíveis.'))};
  tx.onerror=()=>{db.close();reject(tx.error)};
 });
}
export async function createOfflineCustomer(tenant:string,name:string,phone:string):Promise<Customer>{
 const customer:Customer={id:newId(),name:name.trim(),phone:phone.trim()};
 if(!customer.name||customer.name.length>160)throw new Error('Nome inválido.');
 return appendOffline(tenant,{id:newId(),tenant,kind:'customer',tempId:customer.id,name:customer.name,phone:customer.phone??'',createdAt:Date.now()},snapshot=>{
  snapshot.customers.push(customer);return customer;
 });
}
export async function createOfflineVehicle(tenant:string,customerId:string,plate:string,model:string):Promise<Vehicle>{
 const vehicle:Vehicle={id:newId(),customerId,plate:plate.trim().toUpperCase(),model:model.trim()};
 if(!vehicle.plate||vehicle.plate.length>10||!vehicle.model)throw new Error('Dados do veículo inválidos.');
 return appendOffline(tenant,{id:newId(),tenant,kind:'vehicle',tempId:vehicle.id,customerId,plate:vehicle.plate,model:vehicle.model,createdAt:Date.now()},snapshot=>{
  if(!snapshot.customers.some(x=>x.id===customerId))throw new Error('Cliente não encontrado.');
  if(snapshot.vehicles.some(x=>x.plate===vehicle.plate))throw new Error('Placa já cadastrada localmente.');
  snapshot.vehicles.push(vehicle);return vehicle;
 });
}
export async function createOfflineOrder(tenant:string,vehicleId:string,serviceIds:string[]):Promise<Order>{
 const tempId=newId();
 return appendOffline(tenant,{id:newId(),tenant,kind:'order',tempId,vehicleId,serviceIds:[...serviceIds],createdAt:Date.now()},snapshot=>{
  if(!snapshot.vehicles.some(x=>x.id===vehicleId))throw new Error('Veículo não encontrado.');
  const unique=[...new Set(serviceIds)];
  const selected=snapshot.services.filter(x=>unique.includes(x.id));
  if(selected.length!==unique.length||selected.length===0||selected.length>30)throw new Error('Serviços inválidos.');
  const order:Order={id:tempId,number:'OFF-'+tempId.slice(0,8),vehicleId,total:selected.reduce((s,x)=>s+x.price,0),status:0,paymentStatus:0};
  snapshot.orders.unshift(order);return order;
 });
}
export async function listPending(tenant:string):Promise<{id:string;kind:string;description:string;createdAt:number}[]>{
 const all=await store<Pending[]>('pending','readonly',s=>s.getAll());
 return all.filter(x=>x.tenant===tenant).sort((a,b)=>a.createdAt-b.createdAt).map(x=>({
  id:x.id,kind:x.kind,createdAt:x.createdAt,
  description:x.kind==='status'?'Comanda '+x.orderId+': etapa '+x.from+' → '+x.to:
   x.kind==='customer'?'Cliente '+x.name:x.kind==='vehicle'?'Veículo '+x.plate:'Nova comanda '+x.tempId
 }));
}
export async function discardPending(tenant:string,id:string){
 const all=await store<Pending[]>('pending','readonly',s=>s.getAll());
 const item=all.find(x=>x.tenant===tenant&&x.id===id);
 if(!item)throw new Error('Pendência não encontrada.');
 if(item.kind!=='status')throw new Error('Cadastros pendentes dependem de outras operações; sincronize ou solicite suporte antes de descartar.');
 await store('pending','readwrite',s=>s.delete(id));
 // Rebuild local order statuses from the server on next successful reload.
}
export async function pendingCount(tenant:string):Promise<number>{
 const all=await store<Pending[]>('pending','readonly',s=>s.getAll());
 return all.filter(x=>x.tenant===tenant).length;
}
let activeSync:Promise<number>|null=null;
const SYNC_LOCK='atwash-sync-owner';
const SYNC_TTL=120000;
function lockSync():()=>void{
 const owner=crypto.randomUUID();
 const current=localStorage.getItem(SYNC_LOCK);
 if(current){
  try{const lock=JSON.parse(current) as {owner:string;expires:number};
   if(lock.expires>Date.now())throw new Error('Outra aba está sincronizando. Aguarde e tente novamente.');
  }catch(e){if(e instanceof Error&&e.message.startsWith('Outra aba'))throw e}
 }
 localStorage.setItem(SYNC_LOCK,JSON.stringify({owner,expires:Date.now()+SYNC_TTL}));
 if(JSON.parse(localStorage.getItem(SYNC_LOCK)??'{}').owner!==owner)
  throw new Error('Sincronização já iniciada em outra aba.');
 return ()=>{try{if(JSON.parse(localStorage.getItem(SYNC_LOCK)??'{}').owner===owner)localStorage.removeItem(SYNC_LOCK)}catch{/* ignore */}};
}

export async function syncPending(token:string):Promise<number>{
 if(activeSync)return activeSync;
 activeSync=(async()=>{
  if(navigator.locks?.request){
   return navigator.locks.request('atwash-offline-sync',{mode:'exclusive'},()=>performSync(token));
  }
  const release=lockSync();
  try{return await performSync(token)}finally{release()}
 })().finally(()=>{activeSync=null});
 return activeSync;
}
async function performSync(token:string):Promise<number>{
 if(!navigator.onLine)return 0;
 const tenant=tenantFromToken(token);
 let count=0;
 // Process in creation order; remap temporary IDs durably before deleting each item.
 while(true){
  const all=(await store<Pending[]>('pending','readonly',s=>s.getAll()))
   .filter(x=>x.tenant===tenant).sort((a,b)=>a.createdAt-b.createdAt ||
    ({customer:0,vehicle:1,order:2,status:3}[a.kind]-{customer:0,vehicle:1,order:2,status:3}[b.kind]));
  if(all.length===0)break;
  const item=all[0];
  if(item.kind==='status'){
   const server=await api<Order>('/orders/'+item.orderId,token);
   if(server.status===item.to){
    // Already applied by a previous attempt; safe to remove this pending step.
   }else if(server.status!==item.from){
    throw new Error('Conflito na comanda '+server.number+': esperado '+item.from+', servidor '+server.status+'. Revise antes de sincronizar.');
   }else{
    await api('/orders/'+item.orderId+'/status',token,'PATCH',{status:item.to,expectedStatus:item.from});
   }
  }else{
   let realId:string;
   let number:string|undefined;
   if(item.kind==='customer'){
    const result=await api<{id:string}>('/customers',token,'POST',{name:item.name,phone:item.phone,clientId:item.tempId});
    realId=result.id;
   }else if(item.kind==='vehicle'){
    const result=await api<{id:string}>('/vehicles',token,'POST',{customerId:item.customerId,plate:item.plate,model:item.model,clientId:item.tempId});
    realId=result.id;
   }else{
    const result=await api<{id:string;number:string}>('/orders',token,'POST',{vehicleId:item.vehicleId,serviceIds:item.serviceIds,clientId:item.tempId});
    realId=result.id;number=result.number;
   }
   const snapshot=await loadSnapshot(tenant);
   if(snapshot){
    if(item.kind==='customer'){
     const entity=snapshot.customers.find(x=>x.id===item.tempId);if(entity)entity.id=realId;
     snapshot.vehicles.forEach(x=>{if(x.customerId===item.tempId)x.customerId=realId});
    }else if(item.kind==='vehicle'){
     const entity=snapshot.vehicles.find(x=>x.id===item.tempId);if(entity)entity.id=realId;
     snapshot.orders.forEach(x=>{if(x.vehicleId===item.tempId)x.vehicleId=realId});
    }else{
     const entity=snapshot.orders.find(x=>x.id===item.tempId);if(entity){entity.id=realId;entity.number=number??entity.number}
    }
    await saveSnapshot(tenant,snapshot);
   }
   // Rewrite references in dependent pending actions before marking this action complete.
   const remaining=await store<Pending[]>('pending','readonly',s=>s.getAll());
   for(const next of remaining.filter(x=>x.tenant===tenant&&x.id!==item.id)){
    let changed=false;
    if(next.kind==='vehicle'&&next.customerId===item.tempId){next.customerId=realId;changed=true}
    if(next.kind==='order'&&next.vehicleId===item.tempId){next.vehicleId=realId;changed=true}
    if(next.kind==='status'&&next.orderId===item.tempId){next.orderId=realId;changed=true}
    if(changed)await store('pending','readwrite',s=>s.put(next));
   }
  }
  await store('pending','readwrite',s=>s.delete(item.id));
  count++;
 }
 return count;
}
export async function clearOfflineTenant(tenant:string){
 await store('snapshots','readwrite',s=>s.delete(tenant));
 const all=await store<Pending[]>('pending','readonly',s=>s.getAll());
 for(const item of all.filter(x=>x.tenant===tenant))
  await store('pending','readwrite',s=>s.delete(item.id));
}
